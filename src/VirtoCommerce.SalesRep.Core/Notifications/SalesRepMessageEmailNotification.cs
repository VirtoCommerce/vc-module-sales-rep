using VirtoCommerce.NotificationsModule.Core.Model;

namespace VirtoCommerce.SalesRep.Core.Notifications;

/// <summary>
/// Email a Sales Rep sends to a customer's members (VCST-5310 / VCST-5331). Carries the Rep's free-text
/// <see cref="Message"/> and an optional <see cref="Title"/>; the store-scoped template renders them.
/// </summary>
public class SalesRepMessageEmailNotification : EmailNotification
{
    public SalesRepMessageEmailNotification()
        : base(nameof(SalesRepMessageEmailNotification))
    {
    }

    public SalesRepMessageEmailNotification(string type)
        : base(type)
    {
    }

    /// <summary>Optional message title (shown as the heading; the subject template may also use it).</summary>
    [NotificationParameter("Title")]
    public string Title { get; set; }

    /// <summary>The Rep's message body (required). May contain a URL the recipient can click.</summary>
    [NotificationParameter("Message")]
    public string Message { get; set; }
}
