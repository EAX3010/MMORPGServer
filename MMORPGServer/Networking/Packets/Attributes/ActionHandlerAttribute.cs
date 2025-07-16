using MMORPGServer.Common.Enums;

namespace MMORPGServer.Networking.Packets.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class ActionHandlerAttribute : Attribute
    {
        public ActionType ActionType { get; }

        public ActionHandlerAttribute(ActionType actionType)
        {
            ActionType = actionType;
        }
    }
}