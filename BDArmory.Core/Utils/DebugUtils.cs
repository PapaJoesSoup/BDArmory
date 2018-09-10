namespace BDArmory.Core.Utils
{
    class DebugUtils
    {
        private static readonly ScreenMessage  ScreenMessage = new ScreenMessage("", 2, ScreenMessageStyle.LOWER_CENTER);

        public static void DisplayDebugMessage(string message)
        {
            //TODO: Pending of future refactor of BDArmory settings
#if DEBUG
            ScreenMessages.RemoveMessage(ScreenMessage);
            ScreenMessage.message = message;
            ScreenMessages.PostScreenMessage(ScreenMessage);  
#endif
        }
    }
}
