namespace Carbon.Plugins
{
    [Info ( "test_plugin", "test_plugin", "1.1.0" )]
    [Description ( "test_plugin" )]
    public class test_plugin : CarbonPlugin
    {
        private void OnServerInitialized ()
        {
            Puts ( "Hello world!" );
            PrintToChat ("test");
        }
    }


}