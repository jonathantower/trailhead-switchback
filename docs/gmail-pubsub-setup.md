# Gmail Pub/Sub Push Setup

This document describes how to set up Gmail push notifications via Google Cloud Pub/Sub so the Function App receives new-mail events and runs the email processing pipeline.

## Overview

1. Create a Pub/Sub topic in Google Cloud.
2. Create a push subscription that posts to the Function App URL.
3. For each user who connects Gmail, call the Gmail API `users.watch` to start watching their mailbox; Gmail will publish to your topic when mail arrives.
4. The Function App endpoint `POST /webhook/gmail` receives the push, decodes `emailAddress` and `historyId`, looks up the user, fetches new message IDs from history, and processes each message.

## 1. Google Cloud Pub/Sub

1. In [Google Cloud Console](https://console.cloud.google.com/), select the same project used for Gmail OAuth.
2. Enable the **Cloud Pub/Sub API**.
3. Create a topic, e.g. `gmail-push`.
4. Create a **push** subscription:
   - Topic: `gmail-push` (or your topic name).
   - Endpoint URL: `https://YOUR-FUNCTION-APP.azurewebsites.net/api/webhook/gmail` (or your Function App URL + `/api/webhook/gmail`).
   - For local testing, use a tunnel URL (e.g. ngrok): `https://YOUR-TUNNEL.ngrok.io/api/webhook/gmail`.
5. Grant the Gmail API permission to publish to the topic: see [Gmail Push Notifications](https://developers.google.com/gmail/api/guides/push#grant_publish_permissions).

## 2. App setting: Gmail:PubSubTopic

Set **Gmail:PubSubTopic** in the Function App to the full topic name, e.g. `projects/YOUR_PROJECT_ID/topics/gmail-push`. When set:

- **On connect:** When a user connects Gmail (OAuth callback), the app calls Gmail API **users.watch** with this topic and stores the returned `expiration` (epoch ms) for renewal.
- **On disconnect:** The app calls Gmail API **users.stop** and removes the watch record.
- **Renewal:** A timer function runs daily (2:00 UTC) and renews any watch expiring within the next 24 hours by re-calling **users.watch**.

If **Gmail:PubSubTopic** is not set, the app does not call watch on connect; you can still process messages via the manual endpoint: `POST /api/process?userId=...&provider=Gmail&messageId=...`.

## 3. Function App URL

- **Production:** Use the Function App's URL from Terraform output (e.g. `https://func-prod-switchback-xxx.azurewebsites.net`). The webhook route is `/api/webhook/gmail`.
- **Local:** Use `func start` and a tunnel (ngrok, etc.) so Google can POST to your machine. Set the subscription push endpoint to `https://YOUR-TUNNEL.ngrok.io/api/webhook/gmail`.

## 4. Payload Format

The push body (from Pub/Sub) is JSON:

```json
{
  "message": {
    "data": "<base64-encoded JSON>",
    "messageId": "...",
    "publishTime": "..."
  },
  "subscription": "projects/.../subscriptions/..."
}
```

Decoded `data`:

```json
{
  "emailAddress": "user@gmail.com",
  "historyId": "12345"
}
```

The function looks up `userId` by `emailAddress` (via the UserEmail table populated at Gmail connect), fetches message IDs from Gmail history since `historyId`, and runs the pipeline for each message.
