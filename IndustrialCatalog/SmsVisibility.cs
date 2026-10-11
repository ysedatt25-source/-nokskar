public static class SmsVisibility
{
    public static string Apply(string html,HttpContext c,SmsSettingsStore settings){var path=c.Request.Path.Value??"";var active=settings.Active;
        if(path=="/login")return SmsPages.AddOptions(html,active,"login");if(path=="/kayit")return SmsPages.AddOptions(html,active,"register");if(path=="/forgot-password")return SmsPages.AddOptions(html,active,"recover");
        if(path=="/admin/mail"&&AccessControl.Has(c.User,AccessPermissions.System,"view")){var block="<section class='mail-card'><h2>Sağlayıcılar</h2><div class='head-actions'><a class='button secondary' href='/admin/mail'>E-posta sağlayıcı</a><a class='button secondary' href='/admin/sms'>SMS sağlayıcı</a></div>"+(active?" <a class='button' href='/admin/sms/messages'>SMS mesaj yönetimi</a>":"")+"</section>";html=html.Replace("</main>",block+"</main>",StringComparison.Ordinal);}
        if(active&&(path=="/admin"||path=="/admin/customers"||path=="/hesabim/profil"))html=html.Replace("</head>","<link rel='stylesheet' href='/sms-ui.css?v=sms-1'><script defer src='/sms-visibility.js?v=sms-1'></script></head>",StringComparison.Ordinal);
        return html;
    }
}
