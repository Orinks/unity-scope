using System.Collections.Specialized;
using UnityScope.Endpoints;
using UnityScope.Json;

namespace UnityScope.Server
{
    internal struct Response
    {
        public int Status;
        public string Body;
        public static Response Ok(string json) => new Response { Status = 200, Body = json };
        public static Response NotFound(string what) => new Response { Status = 404, Body = $"{{\"error\":\"not_found\",\"what\":\"{what}\"}}" };
        public static Response BadRequest(string msg) => new Response { Status = 400, Body = $"{{\"error\":\"bad_request\",\"message\":\"{msg}\"}}" };
        public static Response Forbidden(string msg) => new Response { Status = 403, Body = $"{{\"error\":\"forbidden\",\"message\":\"{msg}\"}}" };
    }

    internal class RequestRouter
    {
        private readonly MainThreadDispatcher _dispatcher;
        private readonly bool _allowInvoke;

        public RequestRouter(MainThreadDispatcher dispatcher, bool allowInvoke)
        {
            _dispatcher = dispatcher;
            _allowInvoke = allowInvoke;
        }

        public Response Dispatch(string method, string path, NameValueCollection query, string body)
        {
            if (path == "/ping" && method == "GET")
                return Response.Ok(new JsonWriter()
                    .BeginObject()
                    .Field("ok", true)
                    .Field("version", "0.1.0")
                    .EndObject().ToString());

            if (path == "/scene" && method == "GET")
                return _dispatcher.Run(() => SceneEndpoint.Handle());

            if (path == "/tree" && method == "GET")
                return _dispatcher.Run(() => TreeEndpoint.Handle(query));

            if (path == "/node" && method == "GET")
                return _dispatcher.Run(() => NodeEndpoint.Handle(query));

            if (path == "/snapshot" && method == "GET")
                return _dispatcher.Run(() => SnapshotEndpoint.Capture(query));

            if (path == "/snapshot/list" && method == "GET")
                return SnapshotEndpoint.List();

            if (path == "/diff" && method == "GET")
                return _dispatcher.Run(() => DiffEndpoint.Handle(query));

            // /find, /types, /events, /invoke to come. See docs/ARCHITECTURE.md.

            if (path == "/invoke" && method == "POST" && !_allowInvoke)
                return Response.Forbidden("invoke disabled; set UnityScope:AllowInvoke=true in BepInEx config.");

            return Response.NotFound(path);
        }
    }
}
