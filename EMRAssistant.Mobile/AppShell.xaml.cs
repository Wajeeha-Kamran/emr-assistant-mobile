using EMRAssistant.Mobile.Views;

namespace EMRAssistant.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Pages pushed onto the stack rather than switched to must be
        // registered. Register is pushed from Sign in, so "back" returns there.
        Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
        Routing.RegisterRoute(nameof(RecordPage), typeof(RecordPage));
        Routing.RegisterRoute(nameof(ProcessingPage), typeof(ProcessingPage));
        Routing.RegisterRoute(nameof(ReviewPage), typeof(ReviewPage));
        Routing.RegisterRoute(nameof(CodesPage), typeof(CodesPage));
        Routing.RegisterRoute(nameof(SignPage), typeof(SignPage));
    }
}
