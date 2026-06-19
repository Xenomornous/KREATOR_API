using Resend;

namespace Kreator_API.Services;

public class EmailService
{
    private readonly IResend _resend;

    public EmailService(
        IResend resend
    )
    {
        _resend = resend;
    }

    public async Task SendVerificationEmail(
        string email,
        string token
    )
    {
        string verifyUrl =
            $"https://localhost:3000/verify-email?token={token}";

        var message =
            new EmailMessage();

        message.From =
            "onboarding@resend.dev";

        message.To.Add(email);

        message.Subject =
            "Zweryfikuj konto";

        message.HtmlBody =
            $@"
            <h2>Witaj!</h2>

            <p>
                Kliknij link poniżej aby
                zweryfikować konto:
            </p>

            <a href='{verifyUrl}'>
                Zweryfikuj konto
            </a>
            ";

        await _resend.EmailSendAsync(
            message
        );
    }

    public async Task SendPasswordResetEmail(
        string email,
        string token
    )
    {
        string resetUrl =
            $"https://localhost:3000/reset-password?token={token}";

        var message =
            new EmailMessage();

        message.From =
            "onboarding@resend.dev";

        message.To.Add(email);

        message.Subject =
            "Reset hasła";

        message.HtmlBody =
            $@"
            <h2>Reset hasła</h2>

            <p>
                Otrzymaliśmy prośbę o zmianę hasła.
            </p>

            <p>
                Kliknij przycisk poniżej aby
                ustawić nowe hasło:
            </p>

            <a href='{resetUrl}'>
                Ustaw nowe hasło
            </a>

            <p>
                Jeżeli to nie Ty wysłałeś
                tę prośbę, możesz
                zignorować tę wiadomość.
            </p>
            ";

        await _resend.EmailSendAsync(
            message
        );
    }
}