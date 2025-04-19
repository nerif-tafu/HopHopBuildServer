namespace Carbon.Plugins
{
    [Info ( "TestPlugin", "TestPlugin", "1.1.0" )]
    [Description ( "TestPlugin" )]
    public class TestPlugin : CarbonPlugin
    {
        private void OnServerInitialized ()
        {
            Puts ( "Hello world!" );
            PrintToChat ("test");
        }
        
        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if(input.WasJustPressed(BUTTON.FIRE_THIRD)) {
                Puts ( "Hello world!" );
            }
        }
    }
}