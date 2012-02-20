using System;

/*
EchoTracker - Teambox Workspace Manager Protocol (EAnp):

The EAnp protocol carries on the communication between the ET and the KWM. The
ET uses that protocol to execute the actions requested by the user in the UI.
The protocol is therefore designed for real-time interactions. This requirement
has several consequences:

- The data traffic must be kept low so that the KWM can respond quickly to new
  incoming commands from the ET.
  
- The KWM must avoid blocking its UI thread for extended period of time for the
  same reason.

- The state propagation from the KWM to the ET must be inexpensive and
  performed frequently so that the user gets to see up-to-date information.

- The state propagated to the ET must be sufficiently detailed to allow for
  fast, correct and easily-implementable user interactions. For example, the ET
  should be able to populate most of its context menus without having to do an
  information request to the KWM. The list of actions allowed by the context
  menu should match that of the KWM.


The state that must be propagated from the KWM to the ET can be characterized
as follow.

The global view:

The global view includes the list of workspaces, the workspace properties, the
workspace users and some application data such as the list of pending KFS
transactions and the list of screen sharing sessions. The global view must be
kept synchronized with the ET at all times since most user interactions depend
on it.

The client-side events:

Much of the actions that occur in the KWM can be represented as events, e.g.
file X uploaded by user Y in workspace Z. The list of such events is quite
voluminous but it is mostly immutable and it can readily be stored and indexed
by a database.

The KFS views:

There is more than one KFS view per workspace. There are the unified view of
the share and the history view of the share. Both of them are computed by the
KWM.

The unified view of a KFS share is both voluminous and highly mutable. It needs
to be updated in real time because it is directly presented to the user.


The propagation strategy of the state from the KWM to the ET is as follow.

The global view:

The global view is somewhat voluminous, somewhat expensive to recompute and
updated very frequently, making it impractical to recompute it and send it over
the network in full every time it is updated.

Instead, a caching strategy is used. The KWM maintains its own master view
which is always up-to-date. The KWM also maintains a shadow view for each
connected client (ET). The shadow view is updated frequently from the global
view. The frequency is high enough for real time purpose but low enough to keep
the CPU from choking on it.

The KWM uses counters to know which parts of the shadow view it needs to
update. When the shadow view is updated from the master view, the list of
deltas between the two views is extracted and sent to the ET, which applies it
on its own data structures.

The propagation of the global view is automated as much as possible using C#
introspection, so its implementation will likely be less tedious than it may
seem at the first glance.

The client-side events:

The volume of client-side events to propagate may be a large torrent or a mere
trickle. It depends heavily on the actions of the user. Rebuilding or joining a
workspace will cause a lot of events to be propagated. Once the system has
stabilized, some events will be propagated from time to time as the users work.

The propagation model is "pull at your leisure". The KWM uses event ID counters
in the global view to notify the ET of the newly received events. The ET then
pulls those events a bunch at a time to avoid choking the CPU.

In the implementation, the ET sends the last event ID that it is has received
as well as the number of events that it would like to fetch, and the KWM
retrieves and sends those events.

Some events are bound to a workspace. When a workspace is rebuilt from stratch,
the events that were previously received by the ET are now invalid. The KWM
uses "rebuild ID" in the global view to pass that information along. When the
ET detects that the rebuild ID has changed, it purges the associated events
from the database.

The client-side events are used to implement user notifications. Those
notifications are handled entirely by the ET, although it is the KWM that
informs the ET about which events are "fresh" and which events are "stale". 

There is a special kind of client-side events which are called transient 
events. These events have ID 0 and they are sent to the ET immediately when 
they are generated. The transient events are valid until the KWM quits.

The KFS views:

The KFS unified view is difficult to replicate. It is too large to send it all
over the network every time it changes. Using deltas don't help since the move
operations and ACL operations destroy large portions of the tree. The view
changes frequently so it is not possible to amortize the transfer cost over a
longer period of time.

I've thought about using shared memory to lower the transfer cost, but the
transfer of the view is only one of the performance bottlenecks. Recomputing
the view in the first place is quite costly. All the ways that I've considered
to avoid doing so are complex and are not applicable in all cases.

I've thought much about it and I've come to the conclusion that this is a
highly complex problem to solve. I believe the only way to avoid a full
recomputation is to send only a partial view of the tree to the ET.
Unfortunately, this makes the implementation much more complex for both the ET
and the KWM. Also, the content of UI menus may depend on knowing about all
nodes of a view.

Since time is short, I am going to recompute and send the whole view to the ET.
It won't scale with the number of files. If the performance is acceptable for
small shares, we can worry about making it scale better later.

The KWM will notify the ET that the KFS unified view has changed through the
global view. The ET will download the view from the KWM at its leisure.
*/

/* Here is how the 32-bits ANP 'type' field is structured for the
 * EAnp protocol:
 * 
 *    3                   2                   1                   0
 *  1 0 9 8 7 6 5 4 3 2 1 0 9 8 7 6 5 4 3 2 1 0 9 8 7 6 5 4 3 2 1 0
 * +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
 * |Pr type|Rle|                  Namespace ID                     |
 * +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
 * 
 */
namespace kwmlib
{
    /// <summary>
    /// Protocol helper class.
    /// </summary>
    public static class EAnpProto
    {
        // Protocol type.
        public const UInt32 ProtoMask = 0xf0000000;
        public const UInt32 ProtoEAnp = (3 << 28);

        // Role.
        public const UInt32 RoleMask = 0x0C000000;
        public const UInt32 RoleCmd = (0 << 26);
        public const UInt32 RoleRes = (1 << 26);
        public const UInt32 RoleEvt = (2 << 26);

        // Namespace ID.
        public const UInt32 NamespaceMask = 0x03ffffff;

        /// <summary>
        /// Return true if the type specified corresponds to a command.
        /// </summary>
        public static bool IsCmd(UInt32 type)
        {
            return ((type & RoleMask) == RoleCmd);
        }

        /// <summary>
        /// Return true if the type specified corresponds to a reply.
        /// </summary>
        public static bool IsRes(UInt32 type)
        {
            return ((type & RoleMask) == RoleRes);
        }

        /// <summary>
        /// Return true if the type specified corresponds to an event.
        /// </summary>
        public static bool IsEvt(UInt32 type)
        {
            return ((type & RoleMask) == RoleEvt);
        }
    }

    /// <summary>
    /// Flags defined by the KCD to describe a workspace. See the KAnp procotol
    /// for details.
    /// </summary>
    [Flags]
    public enum KwsKcdFlag: uint
    {
        Public = 0x1,
        Freeze = 0x2,
        DeepFreeze = 0x4,
        ThinKfs = 0x8,
        Secure = 0x10,
        MeYou = 0x20,
    }
  
    /// <summary>
    /// Flags defined by the KCD to describe a workspace user. See the KAnp
    /// procotol for details.
    /// </summary>
    [Flags]
    public enum KwsUserKcdFlag: uint
    {
        Admin = 0x01,
        Manager = 0x02,
        Register = 0x04,
        Lock = 0x08,
        Ban = 0x10,
    }
    
    /// <summary>
    /// Flags defined by the KWM to describe a workspace.
    /// </summary>
    [Flags]
    public enum KwsKwmFlag: uint
    {
        /// <summary>
        /// Can display this workspace.
        /// </summary>
        Display = 0x1,
        
        /// <summary>
        /// Can export the workspace credentials.
        /// </summary>
        Export = 0x2,
        
        /// <summary>
        /// Can invite new people in this workspace.   
        /// </summary>
        Invite = 0x4,
        
        /// <summary>
        /// Can set the task to WorkOnline.
        /// </summary>
        WorkOnline = 0x8,
        
        /// <summary>
        /// Can set the task to WorkOffline.
        /// </summary>
        WorkOffline = 0x10,
        
        /// <summary>
        /// Can set the task to Stop.
        /// </summary>
        Stop = 0x20,
        
        /// <summary>
        /// Can set the task to Rebuild.
        /// </summary>
        Rebuild = 0x40,
        
        /// <summary>
        /// Can set the task to DeleteLocally.
        /// </summary>
        DeleteLocally = 0x80,
        
        /// <summary>
        /// Can set the task to DeleteRemotely.
        /// </summary>
        DeleteRemotely = 0x100,
        
        /// <summary>
        /// Can change the value of the secure flag.
        /// </summary>
        SetSecure = 0x200,
        
        /// <summary>
        /// Can change the value of the freeze flag.
        /// </summary>
        SetFreeze = 0x400,
        
        /// <summary>
        /// Can change the value of the deep freeze flag.
        /// </summary>
        SetDeepFreeze = 0x800,
        
        /// <summary>
        /// Can change the vlaue of the thin KFS flag.
        /// </summary>
        SetThinKfs = 0x1000,
        
        /// <summary>
        /// A password must be provided to allow the workspace to log in.
        /// </summary>
        NeedLoginPwd = 0x2000,
        
        /// <summary>
        /// The KWM software must be upgraded to allow the workspace to log in.
        /// </summary>
        MustUpgrade = 0x4000,
        
        /// <summary>
        /// This flag is false if the task is not WorkOnline or WorkOffline. 
        /// If the task is work online, the flag is true if all the events
        /// available on the KCD have been received and applied. If the task
        /// is work offline, the flag is true if all the events stored in the
        /// database have been applied.
        /// </summary>
        KcdEventUpToDate = 0x8000
    }
    
    /// <summary>
    /// Flags defined by the KWM to describe a workspace user.
    /// </summary>
    [Flags]
    public enum KwsUserKwmFlag: uint
    {
        /// <summary>
        /// True if no server event has added this user. It is logically
        /// defined. This holds for the root user and sometimes for the KWM
        /// user.
        /// </summary>
        Virtual = 0x1,
        
        /// <summary>
        /// True if the user is the user using the KWM.
        /// </summary>
        LocalUser = 0x2,
        
        /// <summary>
        /// Make the user administrator.
        /// </summary>
        SetAdmin = 0x4,
        
        /// <summary>
        /// Make the user manager.
        /// </summary>
        SetManager = 0x8,
        
        /// <summary>
        /// Set the value of the lock flag.
        /// </summary>
        SetLock = 0x10,
        
        /// <summary>
        /// Set the value of the ban flag.
        /// </summary>
        SetBan = 0x20,
        
        /// <summary>
        /// Set the password of the user.
        /// </summary>
        SetPwd = 0x40,
        
        /// <summary>
        /// Set the name of the user.
        /// </summary>
        SetName = 0x80
    }

    /// <summary>
    /// Main status of the workspace. 
    /// </summary>
    public enum KwsMainStatus
    {
        /// <summary>
        /// The workspace has not been spawned successfully (yet).
        /// </summary>
        NotYetSpawned,

        /// <summary>
        /// The workspace was created successfully and it is working as 
        /// advertised.
        /// </summary>
        Good,

        /// <summary>
        /// The workspace needs to be rebuilt to be functional. The type
        /// of rebuild required is given by the RebuildFlags field.
        /// </summary>
        RebuildRequired,

        /// <summary>
        /// The workspace has been scheduled for deletion. Kiss it goodbye.
        /// </summary>
        OnTheWayOut
    }

    /// <summary>
    /// Flags describing the type of a workspace rebuild.
    /// </summary>
    [Flags]
    public enum KwsRebuildFlag : uint
    {
        /// <summary>
        /// The cached KCD data must be deleted to rebuild the workspace.
        /// </summary>
        FlushKcdData = 0x1,

        /// <summary>
        /// The local workspace data must be deleted to rebuild the workspace.
        /// </summary>
        FlushLocalData = 0x2
    }

    /// <summary>
    /// Workspace application status.
    /// </summary>
    public enum KwsAppStatus
    {
        /// <summary>
        /// The application is stopped.
        /// </summary>
        Stopped,

        /// <summary>
        /// The application is stopping.
        /// </summary>
        Stopping,

        /// <summary>
        /// The application is started.
        /// </summary>
        Started,

        /// <summary>
        /// The application is starting.
        /// </summary>
        Starting
    }
    
    /// <summary>
    /// Workspace login status.
    /// </summary>
    public enum KwsLoginStatus: uint
    {
        /// <summary>
        /// The user of the workspace is logged out.
        /// </summary>
        LoggedOut,

        /// <summary>
        /// Waiting for logout command reply.
        /// </summary>
        LoggingOut,

        /// <summary>
        /// Waiting for login command reply.
        /// </summary>
        LoggingIn,

        /// <summary>
        /// The user of the workspace is logged in.
        /// </summary>
        LoggedIn
    }

    /// <summary>
    /// Task to perform in the workspace.
    /// </summary>
    public enum KwsTask
    {
        /// <summary>
        /// Stop the workspace. Don't run applications, don't connect to
        /// anything, don't delete anything.
        /// </summary>
        Stop,

        /// <summary>
        /// Create a new workspace or join or import an existing workspace.
        /// </summary>
        Spawn,

        /// <summary>
        /// Work offline.
        /// </summary>
        WorkOffline,

        /// <summary>
        /// Work online.
        /// </summary>
        WorkOnline,

        /// <summary>
        /// Rebuild the workspace.
        /// </summary>
        Rebuild,

        /// <summary>
        /// Delete the workspace locally.
        /// </summary>
        DeleteLocally,

        /// <summary>
        /// Delete the workspace on the server and locally.
        /// </summary>
        DeleteRemotely
    }
    
    /// <summary>
    /// ACL member type.
    /// </summary>
    public enum AclMemberType: uint
    {
        /// <summary>
        /// The member is a user.
        /// </summary>
        User,
        
        /// <summary>
        /// The member is a group.
        /// </summary>
        Group
    }
    
    /// <summary>
    /// ACL permission flags.  
    /// </summary>
    [Flags]
    public enum AclPermFlag: uint
    {
        /// <summary>
        /// Inherit permissions from the parent object.
        /// </summary>
        Inherit = 0x1,
        
        /// <summary>
        /// Read the object.
        /// </summary>
        Read = 0x2,
        
        /// <summary>
        /// Write the object.
        /// </summary>
        Write = 0x4,
        
        /// <summary>
        /// Manage the object.
        /// </summary>
        Manage = 0x8
    }
    
    /// <summary>
    /// ACL group management commands.
    /// </summary>
    public enum AclGroupCmd: uint
    {
        /// <summary>
        /// Create a group.
        /// </summary>
        Create,
        
        /// <summary>
        /// Modify a group.
        /// </summary>
        Modify,
        
        /// <summary>
        /// Delete a group.
        /// </summary>
        Delete
    }

    /// <summary>
    /// KFS client view node status.
    /// </summary>
    public enum KfsClientNodeStatus: uint
    {
        /// <summary>
        /// There exists a file remotely and locally, but the status of the file
        /// cannot be determined at this time.
        /// </summary>
        UndeterminedFile,

        /// <summary>
        /// A file exists remotely but it does not exist locally. Note that this
        /// status does not imply that the file can be downloaded yet. The first
        /// version might still be uploading.
        /// </summary>
        RemoteFile,

        /// <summary>
        /// A file exists locally but no version of it exists on the server.
        /// Note that a user may be uploading the first version.
        /// </summary>
        LocalFile,

        /// <summary>
        /// The current version of a file has been modified locally.
        /// </summary>
        ModifiedCurrent,

        /// <summary>
        /// A stale version of a file has been modified locally, or the user
        /// created a file locally which was already present on the server.
        /// </summary>
        ModifiedStale,

        /// <summary>
        /// The current version of a file exists remotely and locally, and it
        /// has not been modified locally.
        /// </summary>
        UnmodifiedStale,

        /// <summary>
        /// A stale but unmodified version of a file exists locally.
        /// </summary>
        UnmodifiedCurrent,

        /// <summary>
        /// A directory exists locally. 
        /// </summary>
        LocalDirectory,

        /// <summary>
        /// A directory exists remotely. 
        /// </summary>
        RemoteDirectory,

        /// <summary>
        /// A directory exists locally and remotely.
        /// </summary>
        Directory,

        /// <summary>
        /// A directory that exists locally conflicts with a file that exists
        /// remotely.
        /// </summary>
        DirFileConflict,

        /// <summary>
        /// A file that exists locally conflicts with a directory that exists
        /// remotely.
        /// </summary>
        FileDirConflict
    }

    /// <summary>
    /// KFS client view node flag.
    /// </summary>
    [Flags]
    public enum KfsClientNodeFlag: uint
    {
        /// <summary>
        /// A new version of a file is being uploaded.
        /// </summary>
        UploadInProgress = 0x1
    }

    /// <summary>
    /// KFS transaction status.
    /// </summary>
    public enum KfsTransactionStatus: uint
    {
        /// <summary>
        /// The transaction is being executed. 
        /// </summary>
        Started,
        
        /// <summary>
        /// The transaction has completed successfully. 
        /// </summary>
        Success,
        
        /// <summary>
        /// The transaction has failed. 
        /// </summary>
        Failed
    }
    
    /// <summary>
    /// KFS transaction flag. 
    /// </summary>
    [Flags]
    public enum KfsTransactionFlag: uint
    {
        /// <summary>
        /// The transaction can be retried if it has failed or been interrupted.
        /// </summary>
        Retry = 0x1
    }

    /// <summary>
    /// KFS transaction commands. 
    /// </summary>
    public enum KfsTransactionCmd: uint
    {
        /// <summary>
        /// Create and execute a transaction.
        ///   STR    Transaction name.
        ///   UINT32 Number of client operations.
        ///     UINT32 Operation command.
        ///     UINT32 Operation flags.
        ///     <operation-specific data>
        /// </summary>
        Create,
        
        /// <summary>
        /// Stop the transaction but do not remove it from the list.
        ///   UINT64 Transaction ID.
        /// </summary>
        Stop,

        /// <summary>
        /// Stop and remove the transaction from the list. 
        ///   UINT64 Transaction ID.
        /// </summary>
        Clear,

        /// <summary>
        /// Retry the transaction. 
        ///   UINT64 Transaction ID.
        /// </summary>
        Retry,
        
        /// <summary>
        /// Change the freeze status of the KFS share. When the KFS share is
        /// frozen, no server operations are applied on the share. This is
        /// useful when interacting with the user on the KFS share.
        ///   UINT32 True if the share must be frozen.
        /// </summary>
        Freeze
    }

    /// <summary>
    /// KFS client-side operation flags. 
    /// </summary>
    [Flags]
    public enum KfsClientOpFlag: uint
    {
        /// <summary>
        /// If this flag is specified, the operation is performed locally and
        /// not remotely, unless the operation specifies otherwise.
        /// </summary>
        Local = 0x1,

        /// <summary>
        /// If this flag is specified, the operation is performed remotely and
        /// not locally, unless the operation specifies otherwise.
        /// </summary>
        Remote = 0x2,

        /// <summary>
        /// If this flag is specified, external files are not copied but moved
        /// into the share instead.
        /// </summary>
        ExternalMove = 0x4,
        
        /// <summary>
        /// If an operation references a path that is locked, the transaction
        /// aborts unless this flag has been specified. In that case, the
        /// offending operation is skipped.
        /// </summary>
        SkipLockedPath = 0x8,

        /// <summary>
        /// Open the file once it has been downloaded.
        /// </summary>
        OpenAfterDownload = 0x10
    }
    
    /// <summary>
    /// KFS client-side operation commands. 
    /// </summary>
    public enum KfsClientOpCmd: uint
    {
        /// <summary>
        /// Create the directories specified in the share.
        ///   UINT32 Number of paths.
        ///     STR    Relative path.
        /// </summary>
        CreateDir,

        /// <summary>
        /// Create the files specified in the share. Remote implies local.
        ///   UINT32 Number of paths.
        ///     STR    Relative path.
        ///     STR    Extension.
        ///     STR    ProgID?
        /// </summary>
        CreateFile,

        /// <summary>
        /// Delete the paths specified in the share.
        ///   UINT32 Number of paths.
        ///     STR    Relative path.
        /// </summary>
        DeletePath,

        /// <summary>
        /// Move the paths specified in the share.
        ///   UINT32 Number of paths.
        ///     STR    Source relative path.
        ///     STR    Destination relative path.
        /// </summary>
        MovePath,

        /// <summary>
        /// Add files and directories external to the share. Remote implies local.
        ///   UINT32 Number of paths.
        ///     STR    Source absolute path.
        ///     STR    Destination relative path.
        /// </summary>
        AddExternalPath,

        /// <summary>
        /// Upload the specified files.
        ///   UINT32 Number of paths.
        ///     STR    Relative path.
        /// </summary>
        UploadPath,

        /// <summary>
        /// Download the specified files.
        ///   UINT32 Number of paths.
        ///     STR    Relative path.
        /// </summary>
        DownloadPath,
        
        /// <summary>
        /// Download the specified version of a file at the absolute location
        /// specified.
        ///   UINT32 Number of files.
        ///     UINT64 Inode.
        ///     UINT32 Commit ID.
        ///     STR    Destination absolute path.
        /// </summary>
        DownloadInode,
        
        /// <summary>
        /// This command can only be applied to folders. It recursively clears
        /// all the ACL rules of the inodes contained in the specified folders
        /// and sets their 'inherit' flag. The ACL of the specified folders is
        /// unaffected unless recursively modified through the other specified
        /// folders.
        ///   UINT32 Number of inodes.
        ///     UINT64 Inode.
        /// </summary>
        FlattenAcl,
        
        /// <summary>
        /// Set the ACL rules of the specified inodes to the values specified.
        ///   UINT32 Number of inodes.
        ///     UINT64 Inode.
        ///   UINT32 Number of ACL rules to set.
        ///     UINT32 Member ID.
        ///     UINT32 Member type.
        ///     UINT32 Permissions.
        /// </summary>
        SetAcl
    }
    
    /// <summary>
    /// KFS client-side event operation type. 
    /// </summary>
    public enum KfsClientEvtType: uint
    {
        /// <summary>
        /// A file has been uploaded.
        /// </summary>
        FileUploaded,
        
        /// <summary>
        /// A file has been downloaded. Online SKURL only.
        /// </summary>
        FileDownloaded,
        
        /// <summary>
        /// A folder has been created remotely.
        /// </summary>
        FolderCreated,
        
        /// <summary>
        /// A file has been deleted remotely.
        /// </summary>
        FileDeleted,
        
        /// <summary>
        /// A folder has been deleted remotely.
        /// </summary>
        FolderDeleted,
        
        /// <summary>
        /// A file has been moved remotely.
        /// </summary>
        FileMoved,
        
        /// <summary>
        /// A folder has been moved remotely.
        /// </summary>
        FolderMoved
    }

    /// <summary>
    /// Code describing a RegisterKps result.
    /// </summary>
    public enum EAnpRegisterKpsCode : uint
    {
        /// <summary>
        /// Registration successful.
        /// </summary>
        OK,

        /// <summary>
        /// Generic failure.
        /// </summary>
        Failure,

        /// <summary>
        /// The user needs to confirm the registration by clicking on the link
        /// in the confirmation email.
        /// </summary>
        EmailConfirm,

        /// <summary>
        /// Free registration is disabled.
        /// </summary>
        NoAutoRegister,

        /// <summary>
        /// The requested login name was already registered with another
        /// password.
        /// </summary>
        LoginTaken,

        /// <summary>
        /// No license is associated to the user.
        /// </summary>
        NoLicense,

        /// <summary>
        /// The registration succeeded but no KCD server is available.
        /// </summary>
        NoKcd
    }

    /// <summary>
    /// Code describing a quota type.
    /// </summary>
    public enum EAnpQuotaType : uint
    {
        // Catch-all quota type.
        Generic,

        // Per-workspace file quota (not related to the license).
        PerKwsFileQuota,

        // Secure workspace.
        SecureKws
    }

    /// <summary>
    /// EAnp failure types.
    /// </summary>
    public enum EAnpFailType : uint
    {
        /// <summary>
        /// Generic failure.
        /// </summary>
        Generic,

        /// <summary>
        /// The user cancelled the operation.
        /// </summary>
        Cancelled,

        /// <summary>
        /// The operation was interrupted without the user's consent.
        /// </summary>
        Interrupted,

        /// <summary>
        /// Another operation currently in progress is preventing the
        /// operation from being executed.
        /// </summary>
        Concurrent,

        /// <summary>
        /// The connection to the KCD host was lost.
        /// </summary>
        KcdConn,

        /// <summary>
        /// The connection to the EAnp host was lost.
        /// </summary>
        EAnpConn,

        /// <summary>
        /// The KPS configuration of the user is invalid.
        /// </summary>
        InvalidKpsConfig,

        /// <summary>
        /// The login password provided for a workspace is incorrect.
        /// </summary>
        InvalidKwsLoginPwd,

        /// <summary>
        /// Permission denied.
        /// </summary>
        PermDenied,

        /// <summary>
        /// Resource/license quota exceeded.
        ///   UIN32 Quota type.
        /// </summary>
        QuotaExceeded,

        /// <summary>
        /// The KWM must be upgraded.
        /// </summary>
        UpgradeKwm
    }
    
    /// <summary>
    /// EchoTracker - Teambox Workspace Manager Protocol commands.
    /// </summary>
    public enum EAnpCmd: uint
    {
        /// <summary>
        /// Cancel a command in progress. There is no result sent for that
        /// command.
        ///   UINT64 Command ID.
        /// </summary>
        CancelCmd = EAnpProto.ProtoEAnp + EAnpProto.RoleCmd + 1,

        /// <summary>
        /// Register on the KPS.
        ///   UINT32 True if the KWM should use the freemium interface.
        ///   STR    KPS host.
        ///   STR    Login.
        ///   STR    Password.
        /// </summary>
        RegisterKps,
        
        /// <summary>
        /// Modify the workspace list.
        ///   UINT32 Number of folders.
        ///     STR    Full path.
        ///     BIN    ET-specific blob.
        ///   UINT32 Number of workspaces.
        ///     UINT64 Internal workspace ID.
        ///     STR    Full path.
        ///     BIN    ET-specific blob.
        /// </summary>
        ModifyKwsList,
        
        /// <summary>
        /// Export a workspace or the workspace list.
        ///   UINT64 Internal workspace ID. 0 for the whole list.
        ///   STR    Destination path.
        /// </summary>
        ExportKws,

        /// <summary>
        /// Import the workspaces specified.
        ///   STR    String containing the data of the credentials file.
        /// </summary>
        ImportKws,
        
        /// <summary>
        /// Set the current task of a workspace.
        ///   UINT64 Workspace ID.
        ///   UINT32 Workspace task.
        ///   (task-specific data).
        ///
        ///   Rebuild:
        ///     UINT32 Rebuild flags.
        /// </summary>
        SetKwsTask,
        
        /// <summary>
        /// Set the login password.
        ///   UINT64 Workspace ID.
        ///   STR    Password.
        /// </summary>
        SetLoginPwd,
    
        /// <summary>
        /// Create a workspace.
        ///   STR    Name of the workspace.
        ///   UINT32 KCD workspace flags.
        /// </summary>
        CreateKws,
        
        /// <summary>
        /// Invite users to the workspace specified.
        ///   UINT64 Internal workspace ID.
        ///   UINT32 True if the invitation emails should be sent by the KCD.
        ///   STR    Invitation message if invitation emails should be sent by
        ///          the KCD.
        ///   UINT32 Number of people invited.
        ///     STR    User real name.
        ///     STR    User email address.
        ///     UINT64 Key ID. 0 if none.
        ///     STR    Organization name. Empty if none.
        ///     STR    Password. Empty if none.
        /// </summary>
        InviteKws,
        
        /// <summary>
        /// Lookup the specified recipient addresses.
        ///   UINT32 Number of addresses.
        ///     STR    Email address.
        /// </summary>
        LookupRecAddr,

        /// <summary>
        /// Get a SKURL.
        ///   STR    Subject.
        ///   UINT32 Number of recipient.
        ///     STR    Recipient name.
        ///     STR    Recipient email address.
        ///   UINT32 Number of attachments to manage.
        ///     STR    Path to file on local filesystem. The files are owned by
        ///            KWM.
        ///   UINT64 Attachment expiration delay in seconds. 0 is infinite.
        /// </summary>
        GetSkurl,
        
        /// <summary>
        /// Change the properties of a workspace.
        ///   UINT64 Workspace ID.
        ///   UINT32 KCD flags.
        ///   STR    Name.
        /// </summary>
        SetKwsProp,
        
        /// <summary>
        /// Change the properties of a user.
        ///   UINT64 Workspace ID.
        ///   UINT32 User ID.
        ///   UINT32 KCD flags.
        ///   STR    Name.
        /// </summary>
        SetUserProp,
        
        /// <summary>
        /// Set the password of a user.
        ///   UINT64 Workspace ID.
        ///   UINT32 User ID.
        ///   STR    Password.
        /// </summary>
        SetUserPwd,
        
        /// <summary>
        /// Manage a KFS transaction.
        ///   UINT64 Workspace ID.
        ///   UINT32 Transaction command.
        ///   (command-specific data)
        /// </summary>
        KfsManageTransaction,
        
        /// <summary>
        /// Get the current view of the KFS share.
        ///   UINT64 Workspace ID.
        /// </summary>
        KfsGetCurrentView,
        
        /// <summary>
        /// Get the view of the KFS share at the given commit ID.
        ///   UINT64 Workspace ID.
        ///   UINT64 Commit ID.
        /// </summary>
        KfsGetHistoryView,
        
        /// <summary>
        /// Create, modify or delete an ACL group.
        ///   UINT64 Workspace ID.
        ///   UINT32 ACL group command.
        ///   UINT32 Group ID. This is ignored if the group is being created.
        ///   STR    Name of the group. This is ignored if the group is being deleted.
        ///   UINT32 Number of member rules in the group. This is ignored if the group
        ///          is being deleted.
        ///     UINT32 Member ID.
        ///     UINT32 Member type.
        ///     UINT32 Permissions.
        /// </summary>
        AclManageGroup,
        
        /// <summary>
        /// Send a chat message.
        ///   UINT64 Workspace ID.
        ///   UINT32 Channel ID. For a normal workspace this is 0, for a
        ///          public workspace this is the target user ID.
        ///   STR    Chat message.
        /// </summary>
        ChatPostMsg,
        
        /// <summary>
        /// Accept a public chat request.
        ///   UINT64 Workspace ID.
        ///   UINT32 User ID.
        ///   UINT64 Request ID.
        /// </summary>
        PbAcceptChat,
        
        /// <summary>
        /// Create a VNC session.
        ///   UINT64 Workspace ID.
        ///   UINT32 True if control must be given to remote users.
        ///   STR    Subject.
        /// </summary>
        VncCreateSession,
        
        /// <summary>
        /// Join a VNC session.
        ///   UINT64 Workspace ID.
        ///   UINT64 Session ID.
        ///   STR    Subject.
        /// </summary>
        VncJoinSession,

        /// <summary>
        /// Verify that the EAnp event specified has the UUID specified. The
        /// ET must perform this check once per workspace with the last event
        /// ID it has received from that workspace to make sure it didn't cache
        /// stale information. This situation can happen if the KWM quit
        /// unexpectedly and fail to commit in its local database the events
        /// that it has sent to the ET.
        ///   UINT64 Workspace ID.
        ///   UINT64 Event ID.
        ///   BIN    UUID.
        /// </summary>
        CheckEventUuid,

        /// <summary>
        /// Fetch EAnp events from the KWM.
        ///   UINT64 Workspace ID.
        ///   UINT32 Starting event ID.
        ///   UINT32 Maxmium number of events to fetch.
        /// </summary>
        FetchEvent,

        /// <summary>
        /// Fetch an update of the state of the KWM.
        /// </summary>
        FetchState,
    }
 
    /// <summary>
    /// EchoTracker - Teambox Workspace Manager Protocol command results.
    /// </summary>
    public enum EAnpRes: uint
    {
        /// <summary>
        /// Generic success result.
        /// </summary>
        OK = EAnpProto.ProtoEAnp + EAnpProto.RoleRes + 1,
        
        /// <summary>
        /// Generic failure result.
        ///   UINT32 Failure type.
        ///   STR    Error message.
        ///   (failure-specific data).
        /// </summary>
        Failure,

        /// <summary>
        /// KPS registration result.
        ///   UINT32 Register KPS code.
        ///   STR    Error message. Empty if none.
        /// </summary>
        RegisterKps,
        
        /// <summary>
        /// Workspace created.
        ///   UINT64 Internal workspace ID.
        /// </summary>
        CreateKws,

        /// <summary>
        /// Workspaced joined.
        ///   UINT64 Internal workspace ID.
        /// </summary>
        JoinKws,

        /// <summary>
        /// Users invited.
        ///   STR    Workspace-linked email URL.
        ///   UINT32 Number of people invited.
        ///     STR    User email address.
        ///     STR    Invitation URL.
        ///     STR    Inviration error.
        /// </summary>
        InviteKws,
        
        /// <summary>
        /// Looked up the recipient addresses.
        ///   UINT32 Number of addresses.
        ///     STR    Email address.
        ///     UINT64 Key ID. 0 if none.
        ///     STR    Organization name. Empty if none.
        /// </summary>
        LookupRecAddr,
        
        /// <summary>
        /// SKURL result.
        ///   STR    SKURL. Empty if none.
        ///   UINT64 KFS attachment transaction ID. 0 if none.
        /// </summary>
        GetSkurl,

        /// <summary>
        /// The KFS transaction has been created.
        ///   UINT64 Transaction ID.
        /// </summary>
        KfsTransactionCreated,
        
        /// <summary>
        /// The KFS view requested has been computed.
        ///   UINT32 Number of nodes. The first node is the root.
        ///     UINT64 Node ID. This identifier is never recycled for the 
        ///            lifetime of the session.
        ///     UINT64 CreationDate.
        ///     UINT64 LastModifiedDate.
        ///     UINT64 Inode. Valid only if a remote object exists. Note that
        ///            the root has inode 0.
        ///     UINT64 LastVersionCommitID. Commit ID of the last version that
        ///            can be downloaded from the server. 0 if none.
        ///     UINT64 Size.
        ///     UINT32 Status.
        ///     UINT32 Flags.
        ///     UINT32 Permissions.
        ///     UINT32 CreationUserID.
        ///     UINT32 LastModifiedUserID.
        ///     STR    Name.
        ///     UINT32 Number of child nodes.
        ///       UINT64 Node ID.
        /// </summary>
        KfsView,

        /// <summary>
        /// The VNC session creation / joining requested is in progress. The
        /// UUID specified below can be used to track the status of the 
        /// session.
        ///   BIN    Session UUID.
        /// </summary>
        VncSession,

        /// <summary>
        /// EAnp events fetched.
        ///   UINT64 Latest freshness time.
        ///   UINT32 Number of events.
        ///     BIN    Serialized ANP message, including the header.
        /// </summary>
        FetchEvent,

        /// <summary>
        /// KWM state update fetched.
        ///   BIN    Serialized ANP message containing the state update, 
        ///          excluding the header.
        /// </summary>
        FetchState,
    }

    /// <summary>
    /// EchoTracker - Teambox Workspace Manager Protocol events.
    /// Common initial elements:
    ///   UINT64 Workspace ID. 0 if none.
    ///   UINT64 Date.
    ///   UINT32 User ID.
    ///   UINT64 Freshness ID.
    ///   BIN    UUID.
    /// </summary>
    public enum EAnpEvt: uint
    {
        /// <summary>
        /// A local VNC session has started, failed to start or ended.
        ///   BIN    Session UUID.
        ///   UINT64 Session ID as seen by the KCD. 0 if none.
        ///   UINT32 True if this is a server session, false if it is a client
        ///          session.
        ///   UINT32 True if the session is starting, false if it is ending.
        ///   UINT32 True if an error occurred.
        ///   UINT32 Failure type (only present on error).
        ///   STR    Error message.
        ///   (failure-specific data).
        /// </summary>
        LocalVncSession = EAnpProto.ProtoEAnp + EAnpProto.RoleEvt + 1,

        /// <summary>
        /// The state of the KWM has changed. Resynchronize at your leisure.
        /// Transient event.
        /// </summary>
        FetchState,

        /// <summary>
        /// A KFS operation has been applied.
        ///   UINT32 Operation type.
        ///   UINT64 Inode associated to the operation.
        ///   UINT64 Commit ID associated to the operation.
        ///   UINT64 File size, in a file upload operation.
        ///   STR    Full path to the inode after the operation.
        /// </summary>
        KfsOpApplied,
        
        /// <summary>
        /// A chat message has been received.
        ///   STR    Message.
        /// </summary>
        ChatMsgReceived,
        
        /// <summary>
        /// A VNC session has been started on the KCD.
        ///   UINT64 Session ID.
        ///   STR    Subject. 
        /// </summary>
        VncSessionStarted,
        
        /// <summary>
        /// A VNC session has ended on the KCD.
        ///   UINT64 Session ID.
        /// </summary>
        VncSessionEnded,
        
        /// <summary>
        /// A user has requested a chat session in a public workspace.
        ///   UINT64 RequestID.
        ///   STR    Subject.
        /// </summary>
        PbChatRequested,
        
        /// <summary>
        /// A user has requested the creation of a workspace in a public
        /// workspace.
        ///   STR    Subject.
        /// </summary>
        PbKwsRequested
    }


    /////////////////////
    // Helper classes. //
    /////////////////////

    /// <summary>
    /// Exception representing an EAnp failure.
    /// </summary>
    public class EAnpException : Exception
    {
        /// <summary>
        /// Failure type.
        /// </summary>
        public EAnpFailType FailType;

        public EAnpException(EAnpFailType t, String m)
            : base(m)
        {
            FailType = t;
        }

        /// <summary>
        /// Convert an exception to an EAnpException, as needed.
        /// </summary>
        public static EAnpException FromException(Exception ex)
        {
            if (ex is EAnpException) return ex as EAnpException;
            return new EAnpExGeneric(ex.Message);
        }

        /// <summary>
        /// Convert the KAnp reply specified to a EAnp exception.
        /// </summary>
        public static EAnpException FromKAnpReply(AnpMsg m)
        {
            if (m.Type != KAnp.KANP_RES_FAIL) return new EAnpExGeneric("unexpected server response");
            return FromKAnpFailure(m, 0);
        }

        /// <summary>
        /// Convert the KAnp elements specified to a EAnp exception.
        /// </summary>
        public static EAnpException FromKAnpFailure(AnpMsg m, int offset)
        {
            try
            {
                UInt32 t = m.Elements[offset++].UInt32;
                String s = m.Elements[offset++].String;

                if (t == KAnp.KANP_RES_FAIL_PERM_DENIED) return new EAnpExPermDenied(s);

                else if (t == KAnp.KANP_RES_FAIL_FILE_QUOTA_EXCEEDED)
                    return new EAnpExQuotaExceeded(EAnpQuotaType.PerKwsFileQuota, s);

                else if (t == KAnp.KANP_RES_FAIL_RESOURCE_QUOTA)
                {
                    EAnpQuotaType qt = EAnpQuotaType.Generic;
                    UInt32 u = m.Elements[offset++].UInt32;
                    if (u == KAnp.KANP_RESOURCE_QUOTA_NO_SECURE) qt = EAnpQuotaType.SecureKws;
                    return new EAnpExQuotaExceeded(qt, s);
                }

                else return new EAnpExGeneric(s);
            }

            catch (Exception)
            {
                return new EAnpExGeneric("ill formated failure message");
            }
        }

        /// <summary>
        /// Convert the EAnp elements specified to a EAnp exception. This is
        /// used to deserialize EAnp exceptions stored in an ANP message.
        /// </summary>
        public static EAnpException FromEAnpMsg(AnpMsg m, int offset)
        {
            try
            {
                EAnpFailType t = (EAnpFailType)m.Elements[offset++].UInt32;
                String s = m.Elements[offset++].String;

                if (t == EAnpFailType.Generic) return new EAnpExGeneric(s);
                else if (t == EAnpFailType.Cancelled) return new EAnpExCancelled();
                else if (t == EAnpFailType.Interrupted) return new EAnpExInterrupted();
                else if (t == EAnpFailType.Concurrent) return new EAnpExConcurrent();
                else if (t == EAnpFailType.KcdConn) return new EAnpExKcdConn();
                else if (t == EAnpFailType.EAnpConn) return new EAnpExEAnpConn();
                else if (t == EAnpFailType.InvalidKpsConfig) return new EAnpExInvalidKpsConfig();
                else if (t == EAnpFailType.InvalidKwsLoginPwd) return new EAnpExInvalidKwsLoginPwd();
                else if (t == EAnpFailType.PermDenied) return new EAnpExPermDenied(s);
                else if (t == EAnpFailType.QuotaExceeded)
                {
                    EAnpQuotaType qt = (EAnpQuotaType)m.Elements[offset++].UInt32;
                    return new EAnpExQuotaExceeded(qt, s);
                }
                else if (t == EAnpFailType.UpgradeKwm) return new EAnpExUpgradeKwm();
                else return new EAnpExGeneric("unmapped exception");
            }

            catch (Exception)
            {
                return new EAnpExGeneric("ill formated exception message");
            }
        }

        /// <summary>
        /// Add the content of this exception to the ANP message specified.
        /// This is used to serialize an EAnp exception to an ANP message.
        /// </summary>
        public virtual void Serialize(AnpMsg m)
        {
            m.AddUInt32((UInt32)FailType);
            m.AddString(Message);
        }
    }

    public class EAnpExGeneric : EAnpException
    {
        public EAnpExGeneric(String error)
            : base(EAnpFailType.Generic, error)
        { }
    }

    public class EAnpExCancelled : EAnpException
    {
        public EAnpExCancelled()
            : base(EAnpFailType.Cancelled, "the operation was cancelled")
        { }
    }

    public class EAnpExInterrupted : EAnpException
    {
        public EAnpExInterrupted()
            : base(EAnpFailType.Interrupted, "the operation was interrupted")
        { }
    }

    public class EAnpExConcurrent : EAnpException
    {
        public EAnpExConcurrent()
            : base(EAnpFailType.Concurrent, "another operation is already in progress, please try again later")
        { }
    }

    public class EAnpExKcdConn : EAnpException
    {
        public EAnpExKcdConn()
            : base(EAnpFailType.KcdConn, "lost connection to server")
        { }
    }

    public class EAnpExEAnpConn : EAnpException
    {
        public EAnpExEAnpConn()
            : base(EAnpFailType.EAnpConn, "lost connection to internal component")
        {
        }
    }

    public class EAnpExInvalidKpsConfig : EAnpException
    {
        public EAnpExInvalidKpsConfig()
            : base(EAnpFailType.InvalidKpsConfig, "your account configuration is invalid")
        {
        }
    }

    public class EAnpExInvalidKwsLoginPwd : EAnpException
    {
        public EAnpExInvalidKwsLoginPwd()
            : base(EAnpFailType.InvalidKwsLoginPwd, "the password provided is incorrect")
        {
        }
    }

    public class EAnpExPermDenied : EAnpException
    {
        public EAnpExPermDenied(String m)
            : base(EAnpFailType.PermDenied, m)
        {
        }
    }

    public class EAnpExQuotaExceeded : EAnpException
    {
        public EAnpQuotaType QuotaType;

        public EAnpExQuotaExceeded(EAnpQuotaType quotaType, String m)
            : base(EAnpFailType.QuotaExceeded, m)
        {
            QuotaType = quotaType;
        }

        public override void Serialize(AnpMsg m)
        {
            base.Serialize(m);
            m.AddUInt32((uint)QuotaType);
        }
    }

    public class EAnpExUpgradeKwm : EAnpException
    {
        public EAnpExUpgradeKwm()
            : base(EAnpFailType.QuotaExceeded, "the " + KwmStrings.Kwm + " software must be upgraded")
        {
        }
    }
}