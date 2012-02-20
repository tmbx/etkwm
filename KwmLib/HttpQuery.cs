using kcslib;
using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Diagnostics;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace kwmlib
{
    public enum HttpQueryEventType
    {
        /// <summary>
        /// Failed DNS query.
        /// </summary>
        DnsError,

        /// <summary>
        /// The HTTP request failed.
        /// </summary>
        HttpError,

        /// <summary>
        /// The request is done and a result is ready.
        /// </summary>
        Done
    }

    public class HttpQueryEventArgs : EventArgs
    {
        public HttpQueryEventType Type;

        public Exception Ex;

        public HttpQueryEventArgs(HttpQueryEventType ev)
        {
            Type = ev;
        }

        public HttpQueryEventArgs(HttpQueryEventType ev, Exception ex)
        {
            Type = ev;
            Ex = ex;
        }
    }

    class HttpQueryCacheEntry
    {
        public Uri Uri;

        public DateTime DeathTime;

        public string Result;
    }

    /// <summary>
    /// This class represents an asynchronous query made to a HTTP server.
    /// </summary>
    public class HttpQuery
    {
        /// <summary>
        /// The URI that is being fetched.
        /// </summary>
        private Uri m_uri;

        /// <summary>
        /// .NET HTTP query object.
        /// </summary>
        private WebRequest m_query;

        /// <summary>
        /// This is set to True if the query has been cancelled.
        /// </summary>
        private bool m_stopped = false;

        /// <summary>
        /// Content of the fetched URI.
        /// </summary>
        private String m_result;

        /// <summary>
        /// http status code returned.
        /// </summary>
        private HttpStatusCode m_status_code = HttpStatusCode.HttpVersionNotSupported;


        /// <summary>
        /// Set to true to use the build-in caching mechanism.
        /// </summary>
        private bool m_useCache = true;

        public event EventHandler<HttpQueryEventArgs> OnHttpQueryEvent;

        private RemoteCertificateValidationCallback m_cachedValidationCallback = null;

        /// <summary>
        /// Synchronization object for the URI cache.
        /// </summary>
        private static object CacheSync = new object();

        /// <summary>
        /// The URI cache.
        /// </summary>
        private static Dictionary<Uri, HttpQueryCacheEntry> queryCache = new Dictionary<Uri, HttpQueryCacheEntry>();

        /// <summary>
        /// Content of the page returned by the HTTP query. Not null
        /// only if the query was successful.
        /// </summary>
        public string Result
        {
            get
            {
                return m_result;
            }
        }

        public HttpStatusCode StatusCode
        {
            get
            {
                return m_status_code;
            }
        }

        /// <summary>
        /// Return the URI fetched by the query.
        /// </summary>
        public Uri Uri
        {
            get
            {
                return m_uri;
            }
        }

        /// <summary>
        /// Sets wether or not to use the build-in cache mechanism.
        /// </summary>
        public bool UseCache
        {
            set
            {
                m_useCache = value;
            }
        }

        /// <summary>
        /// Build an HTTP query handler for the specified URI.
        /// </summary>
        public HttpQuery(Uri uri)
            : this(uri, null)
        { }

        /// <summary>
        /// Build an HTTP query handler for the specified URI and the given
        /// POST parameters.
        /// </summary>
        public HttpQuery(Uri uri, String postParams)
        {
            m_uri = uri;
            m_query = HttpWebRequest.Create(uri);

            if (!String.IsNullOrEmpty(postParams))
            {
                m_query.Method = "POST";
                m_query.ContentType = "application/x-www-form-urlencoded";
                m_query.ContentLength = postParams.Length;
                using (StreamWriter s = new StreamWriter(m_query.GetRequestStream(), System.Text.Encoding.ASCII))
                {
                    s.Write(postParams);
                }
            }
        }

        /// <summary>
        /// Start the asynchroneous HTTP query.
        /// </summary>
        public void StartQuery()
        {
            // Can't restart a query that was stopped or finished.
            Debug.Assert(m_stopped == false);
            Debug.Assert(m_result == null);

            // Immediately result a result that was previously cached. If using
            // m_useCache = false, the cache will always be empty so the GetCache 
            // call won't return any value, which is what we want.
            String cachedResult = GetCache(m_uri);
            if (cachedResult != null)
            {
                KLogging.Log("Found cached result for " + m_uri.ToString());

                m_result = cachedResult;

                FireOnHttpEvent(new HttpQueryEventArgs(HttpQueryEventType.Done));
                return;
            }

            try
            {
                KLogging.Log("Starting DNS query for " + m_uri.Host);
                // Start an asynchronous DNS request so that the HTTP request doesn't
                // block on DNS resolution.
                Dns.BeginGetHostEntry(m_uri.Host, new AsyncCallback(DnsRequestDone), this);
            }
            catch (Exception ex)
            {
                FireOnHttpEvent(new HttpQueryEventArgs(HttpQueryEventType.DnsError, ex));
                KLogging.LogException(ex);
            }
        }

        /// <summary>
        /// Called on finished DNS request.
        /// </summary>
        private void DnsRequestDone(IAsyncResult res)
        {
            if (!m_stopped)
            {
                try
                {
                    // If the DNS query wasn't successful, bail out.
                    IPHostEntry webAddr = Dns.EndGetHostEntry(res);
                    if (webAddr == null)
                    {
                        FireOnHttpEvent(new HttpQueryEventArgs(HttpQueryEventType.DnsError));
                        KLogging.Log("Failed DNS query for " + m_uri.Host);
                    }
                    else
                    {
                        // Divert the certificate validation callback.
                        KLogging.Log("Done DNS query for " + m_uri.Host);
                        m_cachedValidationCallback = ServicePointManager.ServerCertificateValidationCallback;
                        ServicePointManager.ServerCertificateValidationCallback = ValidateCertificate;

                        // Start the HTTP query.
                        KLogging.Log("Starting HTTP query for " + m_query.RequestUri);
                        m_query.BeginGetResponse(new AsyncCallback(WebRequestDone), this);
                    }
                }
                catch (Exception ex)
                {
                    FireOnHttpEvent(new HttpQueryEventArgs(HttpQueryEventType.HttpError, ex));
                    KLogging.Log(ex.ToString());
                }
            }
            else
            {
                KLogging.Log("Stopping HTTP query to " + m_query.RequestUri + " before HTTP request.");
            }
        }

        /// <summary>
        /// Called on answer to HTTP request.
        /// </summary>        
        private void WebRequestDone(IAsyncResult res)
        {
            if (!m_stopped)
            {
                try
                {
                    WebResponse queryResponse = m_query.EndGetResponse(res);
                    StreamReader responseReader = new StreamReader(queryResponse.GetResponseStream());

                    m_result = responseReader.ReadToEnd();
                    m_status_code = ((HttpWebResponse)queryResponse).StatusCode;
                    if (m_useCache)
                        SetCache(m_uri, m_result);

                    // Reset the default SSL certificate validator.
                    ServicePointManager.ServerCertificateValidationCallback = m_cachedValidationCallback;

                    FireOnHttpEvent(new HttpQueryEventArgs(HttpQueryEventType.Done));

                    KLogging.Log("Done HTTP query to " + m_query.RequestUri);
                }

                catch (WebException webEx)
                {
                    if (webEx.Response != null)
                    {
                        try
                        {
                            // Get the WebException status code.
                            WebExceptionStatus status = webEx.Status;

                            // From Msdn:
                            //  If status is WebExceptionStatus.ProtocolError, 
                            //   there has been a protocol error and a WebResponse 
                            //   should exist.
                            if (status == WebExceptionStatus.ProtocolError)
                            {
                                // Get HttpWebResponse so that to check the HTTP status code.
                                HttpWebResponse httpResponse = (HttpWebResponse)webEx.Response;
                                m_status_code = httpResponse.StatusCode;
                            }

                            StreamReader responseReader = new StreamReader(webEx.Response.GetResponseStream());
                            m_result = responseReader.ReadToEnd();
                        }
                        catch (Exception)
                        {
                        }
                    }
                    FireOnHttpEvent(new HttpQueryEventArgs(HttpQueryEventType.HttpError, webEx));
                }

                catch (Exception ex)
                {
                    FireOnHttpEvent(new HttpQueryEventArgs(HttpQueryEventType.HttpError, ex));
                }
            }

            else
            {
                KLogging.Log("Stopping HTTP query to " + m_query.RequestUri + " after HTTP request.");
            }
        }

        /// <summary>
        /// Call the HTTP query event handler in UI context.
        /// </summary>
        private void FireOnHttpEvent(HttpQueryEventArgs ev)
        {
            KBase.ExecInUI(new KBase.EmptyDelegate(delegate()
            {
                if (OnHttpQueryEvent != null) OnHttpQueryEvent(this, ev);
            }), null);
        }

        /// <summary>
        /// Cancel the query if it is running. This invalidates the object, which
        /// should not be reused after that.
        /// </summary>
        public void Cancel()
        {
            m_stopped = true;
            m_query.Abort();
        }

        /// <summary>
        /// Save an URI in the cache.
        /// </summary>
        private void SetCache(Uri requestUri, string requestResult)
        {
            HttpQueryCacheEntry cacheEntry = new HttpQueryCacheEntry();

            cacheEntry.Uri = requestUri;
            cacheEntry.Result = requestResult;
            // XXX: Is this too much or not enough time?
            cacheEntry.DeathTime = DateTime.Now.AddMinutes(15.0);

            lock (CacheSync)
            {
                queryCache[requestUri] = cacheEntry;
            }
        }

        /// <summary>
        /// Search for the URI in the cache if it's still valid.
        /// </summary>
        private string GetCache(Uri requestUri)
        {
            HttpQueryCacheEntry cacheEntry;

            lock (CacheSync)
            {
                // Search the cache for the URI.
                if (queryCache.ContainsKey(requestUri))
                {
                    cacheEntry = queryCache[requestUri];

                    // Check if the cache entry is still valid.
                    if (cacheEntry.DeathTime < DateTime.Now)
                    {
                        queryCache.Remove(requestUri);
                        cacheEntry = null;
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }

            return cacheEntry.Result;
        }

        /// <summary>
        /// Tell the query object that the result it got was actually invalid, for
        /// reasons that are specific to the caller. This clears the result from the
        /// cache, which actually is what's useful.
        /// </summary>
        public void Invalidate()
        {
            m_result = null;
            RemoveCache(m_uri);
        }

        /// <summary>
        /// Clear an URI from the cache. No-op if the URI to remove isn't
        /// cached.
        /// </summary>
        private void RemoveCache(Uri requestUri)
        {
            lock (CacheSync)
            {
                if (queryCache.ContainsKey(requestUri))
                {
                    queryCache.Remove(requestUri);
                }
            }
        }

        /// <summary>
        /// Complement .NET certificate validation by discarding any
        /// certificate validation error for the requested URI.
        /// </summary>
        private bool ValidateCertificate(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // Shortcut early if this is the host we are connecting to.
            if (sender == m_query) return true;

            // If we have a cached callback, dispatch the request to it.
            if (m_cachedValidationCallback != null)
                return m_cachedValidationCallback(sender, cert, chain, sslPolicyErrors);

            // Return the standard result.
            return (sslPolicyErrors == SslPolicyErrors.None);
        }
    }
}