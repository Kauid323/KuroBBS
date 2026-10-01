// ==============================================================================
// KuroBBS JSBridge Native Implementation (1:1 with Official KuroBBS App)
// 库街区客户端原生通信网桥实现
// ==============================================================================
using System;
using System.Threading.Tasks;

namespace KuroBBS.Shared.Reversed
{
    public class KuroJsBridge
    {
        public static readonly string SchemeProtocol = "kjq";

        public static string BuildPostDeepLink(string postId)
        {
            return $"{SchemeProtocol}://kurobbs/post?postId={postId}";
        }

        public static string BuildTopicDeepLink(string topicId)
        {
            return $"{SchemeProtocol}://kurobbs/topic?topicId={topicId}";
        }

        public static string BuildGameRoleDeepLink(int gameId, string roleId, string serverId)
        {
            return $"{SchemeProtocol}://kurobbs/gameRole?gameId={gameId}&roleId={roleId}&serverId={serverId}";
        }
    }
}