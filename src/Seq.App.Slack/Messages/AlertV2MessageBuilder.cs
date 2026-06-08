using System.Text;
using Seq.App.Slack.Api;
using Seq.App.Slack.Formatting;
using Seq.Apps;
using Seq.Apps.LogEvents;
// ReSharper disable PossibleMultipleEnumeration

namespace Seq.App.Slack.Messages;

class AlertV2MessageBuilder : SlackMessageBuilder
{
    private readonly Host _host;
    private readonly PropertyValueFormatter _propertyValueFormatter;
    private readonly string? _messageTemplate;
    private static readonly HashSet<string> SpecialProperties = ["NamespacedAlertTitle", "Alert", "Source", "SuppressedUntil", "Failures"];

    public AlertV2MessageBuilder(Host host, Apps.App app, PropertyValueFormatter propertyValueFormatter, string? channel, string? username, string? messageTemplate, string? iconUrl, bool excludeOptionalAttachments) 
        : base(app, channel, username, iconUrl, excludeOptionalAttachments)
    {
        _host = host;
        _propertyValueFormatter = propertyValueFormatter ?? throw new ArgumentNullException(nameof(propertyValueFormatter));
        _messageTemplate = messageTemplate;
    }

    protected override string GenerateMessageText(Event<LogEventData> evt)
    {
        var namespacedAlertTitle = EventFormatting.SafeGetProperty(evt, "NamespacedAlertTitle");
        var alertUrl = EventFormatting.SafeGetProperty(evt, "Alert.Url");
        return $"Alert condition triggered by {SlackSyntax.Hyperlink(alertUrl, namespacedAlertTitle)}";
    }

    protected override void AddNecessaryAttachments(SlackMessage message, Event<LogEventData> evt, string color)
    {
        var resultsUrl = EventFormatting.SafeGetProperty(evt, "Source.ResultsUrl");
        var contributingEventsUrl = EventFormatting.SafeGetProperty(evt, "Source.ContributingEventsUrl");

        if (!string.IsNullOrWhiteSpace(contributingEventsUrl))
        {
            // 2026.1+
            var exploreText =
                "Explore " +
                SlackSyntax.Hyperlink(resultsUrl, "detected results") +
                " and " +
                SlackSyntax.Hyperlink(contributingEventsUrl, "contributing events") +
                " in Seq";
            message.Attachments.Add(new SlackMessageAttachment(color, exploreText));
        }
        else
        {
            // 2025.2 and earlier
            var resultsText = SlackSyntax.Hyperlink(resultsUrl, "Explore detected results in Seq");
            message.Attachments.Add(new SlackMessageAttachment(color, resultsText));
        }

        if (_messageTemplate != null)
        {
            message.Attachments.Add(new SlackMessageAttachment(color, _messageTemplate, null, true));
        }

        if (evt.Data.Properties.TryGetValue("Failures", out var f) &&
            f is IEnumerable<object?> failures)
        {
            foreach (var failure in failures)
            {
                var failed = new SlackMessageAttachment(color, SlackSyntax.Escape(failure?.ToString() ?? ""),
                    "Alert Processing Failed");
                message.Attachments.Add(failed);
            }
        }

        var notificationProperties = new SlackMessageAttachment(color);
        foreach (var property in evt.Data.Properties)
        {
            if (SpecialProperties.Contains(property.Key)) continue;
            var value = _propertyValueFormatter.ConvertPropertyValueToString(property.Value);
            notificationProperties.Fields.Add(new SlackMessageAttachmentField(property.Key, value, @short: false));
        }

        if (notificationProperties.Fields.Count != 0)
            message.Attachments.Add(notificationProperties);

        if (evt.Data.Properties.TryGetValue("Source", out var r) &&
            r is IReadOnlyDictionary<string, object?> rd)
        {
            if (rd.TryGetValue("Results", out var rs) &&
                rs is IEnumerable<object?> results &&
                results.Count() > 1 &&
                results.First() is IEnumerable<object?> labelsRow)
            {
                var labels = labelsRow.ToArray();
                if (labels.Length > 1 && labels[0] is "time")
                {
                    var text = new StringBuilder();
                    foreach (var result in results.Skip(1).Cast<IEnumerable<object>>())
                    {
                        var values = result.ToArray();

                        var pre = new StringBuilder();
                        pre.Append(_propertyValueFormatter.ConvertPropertyValueToString(values[0]));
                        pre.Append('\n');
                        for (var i = 1; i < values.Length; ++i)
                        {
                            if (i != 1)
                                pre.Append('\n');
                                
                            var label = labels[i];
                            var value = _propertyValueFormatter.ConvertPropertyValueToString(values[i]);
                            pre.Append($"{label}: {value}");
                        }

                        text.Append(SlackSyntax.Preformatted(pre.ToString()));
                        text.Append('\n');
                    }

                    message.Attachments.Add(new SlackMessageAttachment(color, text.ToString(), "Results"));
                }
            }

            // Contributing events are opted-in per notification, so they're considered minimal (the user can configure
            // the alert to exclude them if desired).
            if (rd.TryGetValue("ContributingEvents", out var ce) &&
                ce is IEnumerable<object?> contributingEvents &&
                contributingEvents.Count() > 1)
            {
                var text = new StringBuilder();
                foreach (var contributing in contributingEvents.Skip(1).Cast<IEnumerable<object?>>())
                {
                    var columns = contributing.ToArray();

                    const int contributingEventsIdIndex = 0,
                        contributingEventsTimestampIndex = 1,
                        contributingEventsMessageIndex = 2;

                    // Timestamp as ISO-8601 string
                    text.Append(SlackSyntax.Code(columns[contributingEventsTimestampIndex] as string ?? ""));
                    text.Append(' ');

                    // Message, linking to event
                    text.Append(SlackSyntax.Hyperlink(EventFormatting.LinkToId(_host, columns[contributingEventsIdIndex] as string ?? ""),
                        SlackSyntax.Escape(columns[contributingEventsMessageIndex] as string ?? "")));
                    text.Append('\n');
                    
                    // Group key values currently ignored, they're included in Results. Some additional formatting
                    // work would be needed if we were to add them here.
                }

                var events = new SlackMessageAttachment(color, text.ToString(), "Contributing Events");
                message.Attachments.Add(events);
            }
        }
    }
}