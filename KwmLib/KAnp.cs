using System;

namespace kwmlib
{
    /* Here is how the 32-bits ANP 'type' field is structured for the
     * KANP protocol :
     * 
     *    3                   2                   1                   0
     *  1 0 9 8 7 6 5 4 3 2 1 0 9 8 7 6 5 4 3 2 1 0 9 8 7 6 5 4 3 2 1 0
     * +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
     * |Pr type|Rle| Namespace ID      | Subtype       | Reserved      |
     * +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
     * 
     * Protocol type :
     * KANP_PROTO
     * 
     * Role :
     * KANP_CMD
     * KANP_RES
     * KANP_EVT
     * 
     * Namespace ID :
     * KANP_NS_GEN
     * KANP_NS_MGT
     * KANP_NS_KWS
     * 
     * KANP_NS_CHAT
     * KANP_NS_KFS
     * KANP_NS_VNC
     * KANP_NS_WB
     * KANP_NS_PB
     * 
     *
     *        PROTOTOL DOCUMENTATION IS ONLY MAINTAINED IN kanp_core_def.h IN 
     *        THE kcd HG REPOSITORY (/home/repos/kcd on nemo).
     *
     */

    /// <summary>
    /// Definition of the Teambox Application Network Protocol.
    /// </summary>
    public abstract class KAnp
    {
        /// <summary>
        /// Protocol major version number.
        /// </summary>
        public const UInt32 Major = 0;
        
        /// <summary>
        /// Protocol minor version number.
        /// </summary>
        public const UInt32 Minor = 6;

        /// <summary>
        /// Last compatible minor version supported on the KWM.
        /// </summary>
        public const UInt32 LastCompMinor = 3;

        /* Protocol type */
        public const UInt32 PROTO_TYPE_MASK = 0xf0000000;

        public const UInt32 KANP_PROTO = (1 << 28);
        public const UInt32 OANP_PROTO = (2 << 28);

        /* Role */
        public const UInt32 ROLE_MASK = 0x0C000000;

        public const UInt32 KANP_CMD = (0 << 26);
        public const UInt32 KANP_RES = (1 << 26);
        public const UInt32 KANP_EVT = (2 << 26);
        public const UInt32 KANP_RESERVED = (3 << 26); /* Reserved */

        /* NamespaceID */
        public const UInt32 NAMESPACE_ID_MASK = 0x03ff0000;
        public const UInt32 KANP_NS_APPS_MIN = (4 << 16);
        public const UInt32 KANP_NS_APPS_MAX = 0xffffffff;

        public const UInt32 KANP_NS_GEN = (0 << 16);
        public const UInt32 KANP_NS_MGT = (1 << 16);
        public const UInt32 KANP_NS_KWS = (2 << 16);
        public const UInt32 KANP_NS_RES = (3 << 16); /* Reserved */
        public const UInt32 KANP_NS_CHAT = (4 << 16);
        public const UInt32 KANP_NS_KFS = (5 << 16);
        public const UInt32 KANP_NS_VNC = (6 << 16);
        public const UInt32 KANP_NS_WB = (7 << 16);
        public const UInt32 KANP_NS_PB = (8 << 16);

        public const UInt32 SUBTYPE_MASK = 0x0000ff00;

        /* Workspace flags. */
        public const UInt32 KANP_KWS_FLAG_PUBLIC = (1 << 0);
        public const UInt32 KANP_KWS_FLAG_FREEZE = (1 << 1);
        public const UInt32 KANP_KWS_FLAG_DEEP_FREEZE = (1 << 2);
        public const UInt32 KANP_KWS_FLAG_THIN_KFS = (1 << 3);
        public const UInt32 KANP_KWS_FLAG_SECURE = (1 << 4);

        /* User flags. */
        public const UInt32 KANP_USER_FLAG_ADMIN = (1 << 0);
        public const UInt32 KANP_USER_FLAG_MANAGER = (1 << 1);
        public const UInt32 KANP_USER_FLAG_REGISTER = (1 << 2);
        public const UInt32 KANP_USER_FLAG_LOCK = (1 << 3);
        public const UInt32 KANP_USER_FLAG_BAN = (1 << 4);

        /* Workspace and user property types. */
        public const UInt32 KANP_PROP_KWS_NAME = 1;
        public const UInt32 KANP_PROP_KWS_FLAGS = 2;
        public const UInt32 KANP_PROP_USER_NAME_ADMIN = 101;
        public const UInt32 KANP_PROP_USER_NAME_USER = 102;
        public const UInt32 KANP_PROP_USER_FLAGS = 103;

        /* Other identifiers. */
        public const UInt32 KANP_KFS_OP_CREATE_FILE = 1;
        public const UInt32 KANP_KFS_OP_CREATE_DIR = 2;
        public const UInt32 KANP_KFS_OP_UPDATE_FILE = 3;
        public const UInt32 KANP_KFS_OP_DELETE_FILE = 4;
        public const UInt32 KANP_KFS_OP_DELETE_DIR = 5;
        public const UInt32 KANP_KFS_OP_MOVE_FILE = 6;
        public const UInt32 KANP_KFS_OP_MOVE_DIR = 7;

        public const UInt32 KANP_KFS_SUBMESSAGE_FILE = 1;
        public const UInt32 KANP_KFS_SUBMESSAGE_CHUNK = 2;
        public const UInt32 KANP_KFS_SUBMESSAGE_COMMIT = 3;
        public const UInt32 KANP_KFS_SUBMESSAGE_ABORT = 4;

        public const UInt32 KANP_KWS_LOGIN_OK = 1;
        public const UInt32 KANP_KWS_LOGIN_OOS = 2;
        public const UInt32 KANP_KWS_LOGIN_BAD_PWD_OR_TICKET = 3;
        public const UInt32 KANP_KWS_LOGIN_BAD_KWS_ID = 4;
        public const UInt32 KANP_KWS_LOGIN_BAD_EMAIL_ID = 5;
        public const UInt32 KANP_KWS_LOGIN_DELETED_KWS = 6;
        public const UInt32 KANP_KWS_LOGIN_ACCOUNT_LOCKED = 7;
        public const UInt32 KANP_KWS_LOGIN_BANNED = 8;

        public const UInt32 KANP_RES_OK = (KANP_PROTO | KANP_RES | KANP_NS_GEN | (0 << 8));
        public const UInt32 KANP_RES_FAIL = (KANP_PROTO | KANP_RES | KANP_NS_GEN | (1 << 8));

        public const UInt32 KANP_RES_FAIL_GEN = 0;
        public const UInt32 KANP_RES_FAIL_BACKEND = 1;
        public const UInt32 KANP_RES_FAIL_CHOOSE_USER_ID = 2;
        public const UInt32 KANP_RES_FAIL_EVT_OUT_OF_SYNC = 3;
        public const UInt32 KANP_RES_FAIL_MUST_UPGRADE = 4;
        public const UInt32 KANP_RES_FAIL_PERM_DENIED = 5;
        public const UInt32 KANP_RES_FAIL_FILE_QUOTA_EXCEEDED = 6;

        public const UInt32 KANP_RES_FAIL_RESOURCE_QUOTA = 7;
        public const UInt32 KANP_RESOURCE_QUOTA_GENERAL = 0;
        public const UInt32 KANP_RESOURCE_QUOTA_NO_SECURE = 1;

        public const UInt32 KANP_CMD_MGT_SELECT_ROLE = (KANP_PROTO | KANP_CMD | KANP_NS_MGT | (0 << 8));
        public const UInt32 KANP_KCD_ROLE_WORKSPACE = 1;
        public const UInt32 KANP_KCD_ROLE_FILE_XFER = 2;
        public const UInt32 KANP_KCD_ROLE_APP_SHARE = 3;
        public const UInt32 KANP_CMD_MGT_CREATE_KWS = (KANP_PROTO | KANP_CMD | KANP_NS_MGT | (1 << 8));
        public const UInt32 KANP_RES_MGT_KWS_CREATED = (KANP_PROTO | KANP_RES | KANP_NS_MGT | (1 << 8));
        public const UInt32 KANP_CMD_KWS_INVITE_KWS = (KANP_PROTO | KANP_CMD | KANP_NS_KWS | (2 << 8));
        public const UInt32 KANP_RES_KWS_INVITE_KWS = (KANP_PROTO | KANP_RES | KANP_NS_KWS | (2 << 8));
        public const UInt32 KANP_CMD_KWS_CONNECT_KWS = (KANP_PROTO | KANP_CMD | KANP_NS_KWS | (3 << 8));
        public const UInt32 KANP_RES_KWS_CONNECT_KWS = (KANP_PROTO | KANP_RES | KANP_NS_KWS | (3 << 8));
        public const UInt32 KANP_CMD_KWS_DISCONNECT_KWS = (KANP_PROTO | KANP_CMD | KANP_NS_KWS | (4 << 8));
        public const UInt32 KANP_CMD_KWS_GET_UURL = (KANP_PROTO | KANP_CMD | KANP_NS_KWS | (5 << 8));
        public const UInt32 KANP_RES_KWS_UURL = (KANP_PROTO | KANP_RES | KANP_NS_KWS | (5 << 8));
        public const UInt32 KANP_CMD_KWS_SET_USER_PWD = (KANP_PROTO | KANP_CMD | KANP_NS_KWS | (6 << 8));
        public const UInt32 KANP_RES_KWS_PROP_CHANGE = (KANP_PROTO | KANP_RES | KANP_NS_KWS | (6 << 8));
        public const UInt32 KANP_CMD_KWS_SET_USER_NAME = (KANP_PROTO | KANP_CMD | KANP_NS_KWS | (7 << 8));
        public const UInt32 KANP_CMD_KWS_SET_USER_ADMIN = (KANP_PROTO | KANP_CMD | KANP_NS_KWS | (8 << 8));
        public const UInt32 KANP_CMD_KWS_SET_USER_MANAGER = (KANP_PROTO | KANP_CMD | KANP_NS_KWS | (9 << 8));
        public const UInt32 KANP_CMD_KWS_SET_USER_LOCK = (KANP_PROTO | KANP_CMD | KANP_NS_KWS | (10 << 8));
        public const UInt32 KANP_CMD_KWS_SET_USER_BAN = (KANP_PROTO | KANP_CMD | KANP_NS_KWS | (11 << 8));
        public const UInt32 KANP_CMD_KWS_SET_NAME = (KANP_PROTO | KANP_CMD | KANP_NS_KWS | (12 << 8));
        public const UInt32 KANP_CMD_KWS_SET_SECURE = (KANP_PROTO | KANP_CMD | KANP_NS_KWS | (13 << 8));
        public const UInt32 KANP_CMD_KWS_SET_FREEZE = (KANP_PROTO | KANP_CMD | KANP_NS_KWS | (14 << 8));
        public const UInt32 KANP_CMD_KWS_SET_DEEP_FREEZE = (KANP_PROTO | KANP_CMD | KANP_NS_KWS | (15 << 8));
        public const UInt32 KANP_CMD_KWS_SET_THIN_KFS = (KANP_PROTO | KANP_CMD | KANP_NS_KWS | (16 << 8));

        public const UInt32 KANP_CMD_CHAT_MSG = (KANP_PROTO | KANP_CMD | KANP_NS_CHAT | (1 << 8));
        public const UInt32 KANP_CMD_KFS_DOWNLOAD_REQ = (KANP_PROTO | KANP_CMD | KANP_NS_KFS | (1 << 8));
        public const UInt32 KANP_RES_KFS_DOWNLOAD_REQ = (KANP_PROTO | KANP_RES | KANP_NS_KFS | (1 << 8));
        public const UInt32 KANP_CMD_KFS_UPLOAD_REQ = (KANP_PROTO | KANP_CMD | KANP_NS_KFS | (2 << 8));
        public const UInt32 KANP_RES_KFS_UPLOAD_REQ = (KANP_PROTO | KANP_RES | KANP_NS_KFS | (2 << 8));
        public const UInt32 KANP_CMD_KFS_DOWNLOAD_DATA = (KANP_PROTO | KANP_CMD | KANP_NS_KFS | (3 << 8));
        public const UInt32 KANP_RES_KFS_DOWNLOAD_DATA = (KANP_PROTO | KANP_RES | KANP_NS_KFS | (3 << 8));
        public const UInt32 KANP_CMD_KFS_PHASE_1 = (KANP_PROTO | KANP_CMD | KANP_NS_KFS | (4 << 8));
        public const UInt32 KANP_RES_KFS_PHASE_1 = (KANP_PROTO | KANP_RES | KANP_NS_KFS | (4 << 8));
        public const UInt32 KANP_CMD_KFS_PHASE_2 = (KANP_PROTO | KANP_CMD | KANP_NS_KFS | (5 << 8));
        public const UInt32 KANP_CMD_VNC_START_TICKET = (KANP_PROTO | KANP_CMD | KANP_NS_VNC | (1 << 8));
        public const UInt32 KANP_RES_VNC_START_TICKET = (KANP_PROTO | KANP_RES | KANP_NS_VNC | (1 << 8));
        public const UInt32 KANP_CMD_VNC_START_SESSION = (KANP_PROTO | KANP_CMD | KANP_NS_VNC | (2 << 8));
        public const UInt32 KANP_RES_VNC_START_SESSION = (KANP_PROTO | KANP_RES | KANP_NS_VNC | (2 << 8));
        public const UInt32 KANP_CMD_VNC_CONNECT_TICKET = (KANP_PROTO | KANP_CMD | KANP_NS_VNC | (3 << 8));
        public const UInt32 KANP_RES_VNC_CONNECT_TICKET = (KANP_PROTO | KANP_RES | KANP_NS_VNC | (3 << 8));
        public const UInt32 KANP_CMD_VNC_CONNECT_SESSION = (KANP_PROTO | KANP_CMD | KANP_NS_VNC | (4 << 8));
        public const UInt32 KANP_CMD_WB_DRAW = (KANP_PROTO | KANP_CMD | KANP_NS_WB | (1 << 8));
        public const UInt32 KANP_CMD_WB_CLEAR = (KANP_PROTO | KANP_CMD | KANP_NS_WB | (2 << 8));
        public const UInt32 KANP_CMD_PB_ACCEPT_CHAT = (KANP_PROTO | KANP_CMD | KANP_NS_PB | (1 << 8));
        public const UInt32 KANP_EVT_KWS_CREATED = (KANP_PROTO | KANP_EVT | KANP_NS_KWS | (1 << 8));
        public const UInt32 KANP_EVT_KWS_INVITED = (KANP_PROTO | KANP_EVT | KANP_NS_KWS | (2 << 8));
        public const UInt32 KANP_EVT_KWS_USER_REGISTERED = (KANP_PROTO | KANP_EVT | KANP_NS_KWS | (3 << 8));
        public const UInt32 KANP_EVT_KWS_DELETED = (KANP_PROTO | KANP_EVT | KANP_NS_KWS | (4 << 8));
        public const UInt32 KANP_EVT_KWS_LOG_OUT = (KANP_PROTO | KANP_EVT | KANP_NS_KWS | (5 << 8));
        public const UInt32 KANP_EVT_KWS_PROP_CHANGE = (KANP_PROTO | KANP_EVT | KANP_NS_KWS | (6 << 8));
        public const UInt32 KANP_EVT_CHAT_MSG = (KANP_PROTO | KANP_EVT | KANP_NS_CHAT | (1 << 8));
        public const UInt32 KANP_EVT_KFS_PHASE_1 = (KANP_PROTO | KANP_EVT | KANP_NS_KFS | (1 << 8));
        public const UInt32 KANP_EVT_KFS_PHASE_2 = (KANP_PROTO | KANP_EVT | KANP_NS_KFS | (2 << 8));
        public const UInt32 KANP_EVT_KFS_DOWNLOAD = (KANP_PROTO | KANP_EVT | KANP_NS_KFS | (3 << 8));
        public const UInt32 KANP_EVT_VNC_START = (KANP_PROTO | KANP_EVT | KANP_NS_VNC | (1 << 8));
        public const UInt32 KANP_EVT_VNC_END = (KANP_PROTO | KANP_EVT | KANP_NS_VNC | (2 << 8));
        public const UInt32 KANP_EVT_WB_DRAW = (KANP_PROTO | KANP_EVT | KANP_NS_WB | (1 << 8));
        public const UInt32 KANP_EVT_WB_CLEAR = (KANP_PROTO | KANP_EVT | KANP_NS_WB | (2 << 8));
        public const UInt32 KANP_EVT_PB_TRIGGER_CHAT = (KANP_PROTO | KANP_EVT | KANP_NS_PB | (1 << 8));
        public const UInt32 KANP_EVT_PB_CHAT_ACCEPTED = (KANP_PROTO | KANP_EVT | KANP_NS_PB | (2 << 8));
        public const UInt32 KANP_EVT_PB_TRIGGER_KWS = (KANP_PROTO | KANP_EVT | KANP_NS_PB | (3 << 8));

        public const UInt32 KANP_KCD_TICKET_DOWNLOAD = 1;
        public const UInt32 KANP_KCD_TICKET_UPLOAD = 2;
        public const UInt32 KANP_KCD_TICKET_VNC_SERVER = 3;
        public const UInt32 KANP_KCD_TICKET_VNC_CLIENT = 4;

        /// <summary>
        /// Return the namespace contained in the type specified.
        /// </summary>
        public static UInt32 GetNsFromType(UInt32 type)
        {
            return (type & KAnp.NAMESPACE_ID_MASK);
        }

        public static String TypeToString(UInt32 type)
        {
            String res;

            switch (type & PROTO_TYPE_MASK)
            {
                case KANP_PROTO:
                    res = "KANP_PROTO";
                    break;
                default:
                    res = "UNKNOWN_PROTO";
                    return res;
            }
            switch (type & ROLE_MASK)
            {
                case KANP_CMD:
                    res += " | KANP_CMD";
                    break;
                case KANP_EVT:
                    res += " | KANP_EVT";
                    break;
                case KANP_RES:
                    res += " | KANP_RES";
                    break;
                default:
                    res += " | UNKNOWN_ROLE";
                    return res;
            }
            switch (type & NAMESPACE_ID_MASK)
            {
                case KANP_NS_GEN:
                    res += " | KANP_NS_GEN";
                    break;
                case KANP_NS_MGT:
                    res += " | KANP_NS_MGT";
                    break;
                case KANP_NS_KWS:
                    res += " | KANP_NS_KWS";
                    break;
                case KANP_NS_RES:
                    res += " | KANP_NS_RES";
                    break;
                case KANP_NS_CHAT:
                    res += " | KANP_NS_CHAT";
                    break;
                case KANP_NS_VNC:
                    res += " | KANP_NS_APP_SHARING";
                    break;
                case KANP_NS_KFS:
                    res += " | KANP_NS_KFS";
                    break;
                case KANP_NS_WB:
                    res += " | KANP_NS_WB";
                    break;
                case KANP_NS_PB:
                    res += " | KANP_NS_PB";
                    break;
                default:
                    res += " | UNKNOWN_NS";
                    return res;
            }

            res += " | " + ((type & SUBTYPE_MASK) >> 8);
            return res + "   " + type;
        }

        /// <summary>
        /// Return the displayable name of a given application. 
        /// KAnp 'type' field must be masked with KAnp.NAMESPACE_ID_MASK in
        /// order to get the right application ID.
        /// </summary>
        public static string GetApplicationName(UInt32 application)
        {
            switch (application)
            {
                case KAnp.KANP_NS_CHAT: return "Message board";
                case KAnp.KANP_NS_KFS: return "Files";
                case KAnp.KANP_NS_VNC: return "Screen sharing";
                case KAnp.KANP_NS_WB: return "Whiteboard";
                case KAnp.KANP_NS_PB: return "Attachment Management";
                default: return "Unknown";
            }
        }
    }
}
