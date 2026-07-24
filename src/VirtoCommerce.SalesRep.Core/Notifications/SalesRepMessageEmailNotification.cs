using VirtoCommerce.NotificationsModule.Core.Model;

namespace VirtoCommerce.SalesRep.Core.Notifications;

public class SalesRepMessageEmailNotification : EmailNotification
{
    public SalesRepMessageEmailNotification()
        : base(nameof(SalesRepMessageEmailNotification))
    {
    }

    [NotificationParameter("Title")]
    public string Title { get; set; }

    [NotificationParameter("Message")]
    public string Message { get; set; }
}
