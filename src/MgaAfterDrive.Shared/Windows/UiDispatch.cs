namespace MgaAfterDrive.Windows;

public static class UiDispatch
{
    public static void BeginInvoke(System.Windows.Threading.Dispatcher dispatcher, Action action)
    {
        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        try
        {
            dispatcher.BeginInvoke(action);
        }
        catch (InvalidOperationException)
        {
        }
    }
}
