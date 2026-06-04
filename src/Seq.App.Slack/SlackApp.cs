using Seq.Apps;
using Seq.Apps.LogEvents;
using Seq.App.Slack.Api;
using Seq.App.Slack.Formatting;
using Seq.App.Slack.Messages;
using Seq.App.Slack.Suppression;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global, UnusedAutoPropertyAccessor.Global, MemberCanBePrivate.Global

namespace Seq.App.Slack;

[SeqApp("Slack Notifier", Description = "Sends events to a Slack channel.")]
public class SlackApp : SeqApp, ISubscribeToAsync<LogEventData>
{
    private const uint AlertV1EventType = 0xA1E77000, AlertV2EventType = 0xA1E77001;

    private Dictionary<uint, SlackMessageBuilder>? _messageBuilders;
    private SlackMessageBuilder? _defaultMessageBuilder;

    [SeqAppSetting(
        DisplayName = "Webhook URL",
        HelpText = "Add the Incoming WebHooks app to your Slack to get this URL.")]
    public required string WebhookUrl { get; init; }

    [SeqAppSetting(
        DisplayName = "Channel",
        IsOptional = true,
        HelpText = "The channel to be used for the Slack notification. If not specified, uses the webhook default.")]
    public string? Channel { get; init; }

    [SeqAppSetting(
        DisplayName = "App name",
        IsOptional = true,
        HelpText = "The name that Seq uses when posting to Slack. If not specified, uses the name of the Seq app instance. The name can also be read from a property by using the format [PropertyName].")]
    public string? Username { get; init; }

    [SeqAppSetting(
        DisplayName = "Suppression time (minutes)",
        IsOptional = true,
        HelpText = "Once an event type has been sent to Slack, the time to wait before sending again. The default is zero.")]
    public int SuppressionMinutes { get; init; } = 0;

    [SeqAppSetting(
        DisplayName = "Exclude optional attachments",
        IsOptional = true,
        HelpText = "Should event property information and other optional attachments be excluded from the message? The default is to attach all properties.")]
    public bool ExcludePropertyInformation { get; init; }

    [SeqAppSetting(
        DisplayName = "Message",
        HelpText = "The message to send to Slack. Refer to https://api.slack.com/docs/formatting for formatting options. Event property values can be added in the format [PropertyName]. The default is \"[RenderedMessage]\". Added as a Markdown attachment for Alerts.",
        IsOptional = true)]
    public string? MessageTemplate { get; init; }

    [SeqAppSetting(
        DisplayName = "Icon URL",
        HelpText = "The image to show in the channel for the message. The default is " + SlackMessageBuilder.DefaultIconUrl + ".",
        IsOptional = true)]
    public string? IconUrl { get; init; }

    [SeqAppSetting(
        DisplayName = "Proxy Server",
        HelpText = "Proxy server to be used when making HTTPS requests to the Slack API. Uses default credentials.",
        IsOptional = true)]
    public string? ProxyServer { get; init; }

    [SeqAppSetting(
        DisplayName = "Maximum property length",
        IsOptional = true,
        HelpText = "If a property when converted to a string is longer than this number it will be truncated.")]
    public int? MaxPropertyLength { get; init; }

    [SeqAppSetting(
        DisplayName = "Included properties",
        IsOptional = true,
        HelpText = "Comma separated list of properties to include as attachments. The default is to include all properties.")]
    public string? IncludedProperties { get; init; }

    private EventTypeSuppressions? _suppressions;
    private ISlackApi? _slackApi;

    // Used reflectively by the app host.
    // ReSharper disable once UnusedMember.Global
    public SlackApp()
    {
    }

    internal SlackApp(ISlackApi slackApi)
    {
        _slackApi = slackApi;
    }

    protected override void OnAttached()
    {
        _slackApi ??= new SlackApi(ProxyServer);

        var propertyValueFormatter = new PropertyValueFormatter(MaxPropertyLength);

        _messageBuilders = new Dictionary<uint, SlackMessageBuilder>
        { 
            [AlertV1EventType] = new AlertV1MessageBuilder(App, Channel, Username, MessageTemplate, IconUrl, ExcludePropertyInformation),
            [AlertV2EventType] = new AlertV2MessageBuilder(Host, App, propertyValueFormatter, Channel, Username, MessageTemplate, IconUrl, ExcludePropertyInformation)
        };

        var includedProperties = string.IsNullOrWhiteSpace(IncludedProperties) ? Array.Empty<string>() : IncludedProperties.Split(',').Select(x => x.Trim());
            
        _defaultMessageBuilder = new DefaultMessageBuilder(Host, App, propertyValueFormatter, Channel, Username,
            IconUrl, MessageTemplate, ExcludePropertyInformation, includedProperties);
    }

    public async Task OnAsync(Event<LogEventData> evt)
    {
        _suppressions ??= new EventTypeSuppressions(SuppressionMinutes);
        if (_suppressions.ShouldSuppressAt(evt.EventType, DateTime.UtcNow))
            return;

        if (!_messageBuilders!.TryGetValue(evt.EventType, out var builder))
            builder = _defaultMessageBuilder!;

        var message = builder.BuildMessage(evt);

        await _slackApi!.SendMessageAsync(WebhookUrl, message);
    }
}