using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace TiaMcpServer.ModelContextProtocol
{
    public sealed partial class McpServer
    {
        [McpServerTool(Name = "ReadApiEvents", ReadOnly = true), Description("Read retained observations from a session event subscription. afterSequence is the last processed sequence, initially 0. The bounded ring reports dropped events. Synchronous native decisions require a callback policy at subscription time.")]
        public static object ReadApiEvents(string subscriptionId, long afterSequence = 0, int limit = PageRequest.DefaultLimit)
        {
            return ProjectCall(() => Portal.ReadApiEvents(subscriptionId, afterSequence, limit));
        }

#if TIA_MCP_READ_WRITE
        [McpServerTool(Name = "SubscribeApiEvent", ReadOnly = false, Destructive = true), Description("Subscribe to an exact event schema from DescribeApiObject. Optional declarative callback handles synchronous native decisions. Changes connection state and may cause native effects. Empty expectedProjectPath is allowed only with no open project.")]
        public static object SubscribeApiEvent(string eventId, string expectedProjectPath, bool acceptNativeEffects, string entityRef = "$portal", JsonElement callback = default)
        {
            return ProjectCall(() => Portal.SubscribeApiEvent(entityRef, eventId, callback, expectedProjectPath, acceptNativeEffects));
        }

        [McpServerTool(Name = "UnsubscribeApiEvent", ReadOnly = false), Description("Remove a native event subscription from this Portal session. Supply the current expected project path, or empty when no project is open.")]
        public static object UnsubscribeApiEvent(string subscriptionId, string expectedProjectPath)
        {
            return ProjectCall(() => Portal.UnsubscribeApiEvent(subscriptionId, expectedProjectPath));
        }
#endif
    }
}
