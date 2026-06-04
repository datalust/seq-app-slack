# Seq.App.Slack [![NuGet](https://img.shields.io/nuget/v/Seq.App.Slack.svg?style=flat-square)](https://www.nuget.org/packages/Seq.App.Slack/)

An app for [Seq](https://datalust.co/seq) that forwards messages to [Slack](https://slack.com).

### Getting started

 1. Install the app into Seq through the Seq UI: _Settings_ > _Apps_ > _Install from NuGet_; the package id is _Seq.App.Slack_
 2. In Slack, select _Admin_ > _Apps and Workflows_ > _Build_ > _Create new App_ > _From Scratch_
 3. In the app registration, choose _Incoming WebHooks_ (this is the new endpoint, not the legacy one)
 4. Add a new incoming webhook configuration and copy the _Webhook URL_
 5. Back in Seq, under _Settings_ > _Apps_, select _Add Instance_ next to the Slack app icon
 6. Configure the app instance, providing the webhook URL

Consult the Seq documentation for further information about [installing Seq apps](https://docs.datalust.co/docs/installing-seq-apps).

For more information see [Notifying with Slack](https://docs.datalust.co/docs/slack-notifications).
