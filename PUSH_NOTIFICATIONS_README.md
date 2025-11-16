# Push Notifications System

This document explains how to set up and use the push notifications system for iOS (APNs), Android (FCM), and Web Push notifications.

## Table of Contents

1. [Overview](#overview)
2. [Database Schema](#database-schema)
3. [API Endpoints](#api-endpoints)
4. [Configuration](#configuration)
5. [Mobile Integration](#mobile-integration)
6. [Sending Notifications](#sending-notifications)
7. [Examples](#examples)

## Overview

The AuthScape push notifications system supports:
- **iOS** - Apple Push Notification service (APNs)
- **Android** - Firebase Cloud Messaging (FCM)
- **Web** - Web Push API with VAPID

Key features:
- Device registration and management
- Multi-platform support
- Automatic token refresh
- Failed delivery tracking
- Batch notifications
- Per-user device management

## Database Schema

### DeviceRegistration Table

The `DeviceRegistrations` table stores all registered devices:

| Column | Type | Description |
|--------|------|-------------|
| Id | long | Primary key |
| UserId | long | User ID (FK to AspNetUsers) |
| DeviceToken | string(500) | FCM/APNs device token |
| Platform | enum | iOS, Android, or Web |
| DeviceName | string(200) | Device model/name |
| OsVersion | string(50) | OS version |
| AppVersion | string(50) | App version |
| Locale | string(10) | Device locale (e.g., "en-US") |
| TimeZoneId | string(100) | Time zone identifier |
| IsActive | bool | Whether device should receive notifications |
| CreatedAt | DateTimeOffset | Registration timestamp |
| LastUpdatedAt | DateTimeOffset | Last update timestamp |
| LastNotificationSentAt | DateTimeOffset? | Last successful notification |
| FailedAttempts | int | Failed delivery count |
| WebPushEndpoint | string(1000) | Web push endpoint (Web only) |
| WebPushP256DH | string(200) | P256DH key (Web only) |
| WebPushAuth | string(200) | Auth key (Web only) |
| Metadata | string | JSON metadata |

### Running the Migration

To create the database table, run:

```bash
cd API
dotnet ef database update --context DatabaseContext --project ../Services.Database
```

## API Endpoints

All endpoints require Bearer token authentication.

### 1. Register Device

**POST** `/api/PushNotifications/register`

Register or update a device for push notifications.

**Request Body:**
```json
{
  "deviceToken": "fcm_token_or_apns_token",
  "platform": 1,  // 1=iOS, 2=Android, 3=Web
  "deviceName": "iPhone 14 Pro",
  "osVersion": "iOS 17.2",
  "appVersion": "1.0.0",
  "locale": "en-US",
  "timeZoneId": "America/New_York",
  "metadata": "{\"custom\":\"data\"}"
}
```

**For Web Push:**
```json
{
  "deviceToken": "web_push_subscription",
  "platform": 3,
  "webPushEndpoint": "https://fcm.googleapis.com/fcm/send/...",
  "webPushP256DH": "BKrW...",
  "webPushAuth": "GpZq..."
}
```

**Response:**
```json
{
  "deviceId": 123,
  "success": true,
  "message": "Device registered successfully",
  "isNewDevice": true
}
```

### 2. Unregister Device

**POST** `/api/PushNotifications/unregister`

Unregister a device (soft delete - marks as inactive).

**Request Body:**
```json
"fcm_token_or_apns_token"
```

**Response:**
```json
{
  "success": true,
  "message": "Device unregistered successfully"
}
```

### 3. Get User Devices

**GET** `/api/PushNotifications/devices`

Get all active devices for the current user.

**Response:**
```json
[
  {
    "id": 123,
    "platform": 2,
    "deviceName": "Samsung Galaxy S23",
    "osVersion": "Android 14",
    "appVersion": "1.0.0",
    "createdAt": "2025-01-15T10:30:00Z",
    "lastUpdatedAt": "2025-01-15T10:30:00Z",
    "lastNotificationSentAt": "2025-01-15T11:00:00Z"
  }
]
```

### 4. Delete Device

**DELETE** `/api/PushNotifications/devices/{deviceId}`

Permanently delete a device registration.

**Response:**
```json
{
  "success": true,
  "message": "Device deleted successfully"
}
```

## Configuration

Add the following to your `appsettings.json`:

```json
{
  "PushNotifications": {
    "APNs": {
      "KeyId": "YOUR_APNS_KEY_ID",
      "TeamId": "YOUR_TEAM_ID",
      "BundleId": "com.yourapp.bundle",
      "KeyPath": "path/to/AuthKey_XXXXX.p8",
      "IsProduction": false
    },
    "FCM": {
      "ServerKey": "YOUR_FCM_SERVER_KEY",
      "SenderId": "YOUR_SENDER_ID"
    },
    "WebPush": {
      "PublicKey": "YOUR_VAPID_PUBLIC_KEY",
      "PrivateKey": "YOUR_VAPID_PRIVATE_KEY",
      "Subject": "mailto:your-email@example.com"
    }
  }
}
```

### Getting FCM Server Key

1. Go to [Firebase Console](https://console.firebase.google.com/)
2. Select your project
3. Go to Project Settings → Cloud Messaging
4. Copy the Server Key

### Getting APNs Keys

1. Go to [Apple Developer Portal](https://developer.apple.com/account/)
2. Certificates, Identifiers & Profiles → Keys
3. Create a new key with APNs enabled
4. Download the `.p8` file

### Generating VAPID Keys (Web Push)

```bash
npx web-push generate-vapid-keys
```

## Mobile Integration

### iOS (Swift)

```swift
import UserNotifications
import FirebaseMessaging

// Request permission
UNUserNotificationCenter.current().requestAuthorization(options: [.alert, .sound, .badge]) { granted, error in
    if granted {
        DispatchQueue.main.async {
            UIApplication.shared.registerForRemoteNotifications()
        }
    }
}

// Get FCM token
Messaging.messaging().token { token, error in
    guard let token = token else { return }
    self.registerDeviceWithServer(token: token)
}

// Register with server
func registerDeviceWithServer(token: String) {
    let device = [
        "deviceToken": token,
        "platform": 1,
        "deviceName": UIDevice.current.name,
        "osVersion": "iOS \(UIDevice.current.systemVersion)",
        "appVersion": Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String ?? "1.0",
        "locale": Locale.current.identifier,
        "timeZoneId": TimeZone.current.identifier
    ] as [String : Any]

    // POST to /api/PushNotifications/register with Bearer token
    // ... your API call here
}
```

### Android (Kotlin)

```kotlin
import com.google.firebase.messaging.FirebaseMessaging

// Get FCM token
FirebaseMessaging.getInstance().token.addOnCompleteListener { task ->
    if (task.isSuccessful) {
        val token = task.result
        registerDeviceWithServer(token)
    }
}

// Register with server
fun registerDeviceWithServer(token: String) {
    val device = mapOf(
        "deviceToken" to token,
        "platform" to 2,
        "deviceName" to "${Build.MANUFACTURER} ${Build.MODEL}",
        "osVersion" to "Android ${Build.VERSION.RELEASE}",
        "appVersion" to BuildConfig.VERSION_NAME,
        "locale" to Locale.getDefault().toLanguageTag(),
        "timeZoneId" to TimeZone.getDefault().id
    )

    // POST to /api/PushNotifications/register with Bearer token
    // ... your API call here
}
```

### Web (JavaScript)

```javascript
// Request permission
const permission = await Notification.requestPermission();

if (permission === 'granted') {
  // Register service worker
  const registration = await navigator.serviceWorker.register('/sw.js');

  // Subscribe to push
  const subscription = await registration.pushManager.subscribe({
    userVisibleOnly: true,
    applicationServerKey: 'YOUR_VAPID_PUBLIC_KEY'
  });

  // Register with server
  const device = {
    deviceToken: JSON.stringify(subscription),
    platform: 3,
    deviceName: navigator.userAgent,
    osVersion: navigator.platform,
    webPushEndpoint: subscription.endpoint,
    webPushP256DH: arrayBufferToBase64(subscription.getKey('p256dh')),
    webPushAuth: arrayBufferToBase64(subscription.getKey('auth')),
    locale: navigator.language,
    timeZoneId: Intl.DateTimeFormat().resolvedOptions().timeZone
  };

  // POST to /api/PushNotifications/register with Bearer token
  await fetch('/api/PushNotifications/register', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${yourAuthToken}`
    },
    body: JSON.stringify(device)
  });
}
```

## Sending Notifications

### From Code (C#)

```csharp
// Inject IMobileServices
private readonly IMobileServices _mobileServices;

// Send to single user
await _mobileServices.SendPushNotificationToUserAsync(
    userId: 123,
    title: "New Message",
    body: "You have a new message from John",
    data: new Dictionary<string, string> {
        { "messageId", "456" },
        { "conversationId", "789" }
    }
);

// Send to multiple users
await _mobileServices.SendPushNotificationToUsersAsync(
    userIds: new List<long> { 123, 456, 789 },
    title: "System Update",
    body: "The system will be down for maintenance"
);

// Advanced send with all options
await _mobileServices.SendPushNotificationAsync(new SendPushNotificationRequest
{
    UserIds = new List<long> { 123 },
    Title = "Order Shipped",
    Body = "Your order #12345 has been shipped",
    ImageUrl = "https://example.com/image.jpg",
    ClickAction = "/orders/12345",
    Badge = 1,
    Sound = "notification.mp3",
    Priority = "high",
    Data = new Dictionary<string, string> {
        { "orderId", "12345" },
        { "trackingNumber", "TRK123456" }
    }
});
```

## Examples

### Complete iOS Registration Flow

```swift
class PushNotificationManager {
    func setup() {
        // 1. Request permission
        UNUserNotificationCenter.current().requestAuthorization(
            options: [.alert, .sound, .badge]
        ) { granted, error in
            if granted {
                DispatchQueue.main.async {
                    UIApplication.shared.registerForRemoteNotifications()
                }
            }
        }

        // 2. Get FCM token
        Messaging.messaging().token { token, error in
            guard let token = token else { return }
            Task {
                await self.registerWithServer(token: token)
            }
        }
    }

    func registerWithServer(token: String) async {
        let url = URL(string: "https://your-api.com/api/PushNotifications/register")!
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.setValue("Bearer \(yourAuthToken)", forHTTPHeaderField: "Authorization")

        let device: [String: Any] = [
            "deviceToken": token,
            "platform": 1,
            "deviceName": UIDevice.current.name,
            "osVersion": "iOS \(UIDevice.current.systemVersion)",
            "appVersion": Bundle.main.infoDictionary?["CFBundleShortVersionString"] as? String ?? "1.0",
            "locale": Locale.current.identifier,
            "timeZoneId": TimeZone.current.identifier
        ]

        request.httpBody = try? JSONSerialization.data(withJSONObject: device)

        let (data, response) = try? await URLSession.shared.data(for: request)
        // Handle response...
    }
}
```

### Complete Android Registration Flow

```kotlin
class PushNotificationManager(private val context: Context, private val apiService: ApiService) {

    fun setup() {
        FirebaseMessaging.getInstance().token.addOnCompleteListener { task ->
            if (task.isSuccessful) {
                val token = task.result
                CoroutineScope(Dispatchers.IO).launch {
                    registerWithServer(token)
                }
            }
        }
    }

    private suspend fun registerWithServer(token: String) {
        val device = DeviceRegistrationRequest(
            deviceToken = token,
            platform = 2,
            deviceName = "${Build.MANUFACTURER} ${Build.MODEL}",
            osVersion = "Android ${Build.VERSION.RELEASE}",
            appVersion = context.packageManager.getPackageInfo(context.packageName, 0).versionName,
            locale = Locale.getDefault().toLanguageTag(),
            timeZoneId = TimeZone.getDefault().id
        )

        try {
            val response = apiService.registerDevice(device)
            // Handle response...
        } catch (e: Exception) {
            // Handle error...
        }
    }
}

// Retrofit API interface
interface ApiService {
    @POST("api/PushNotifications/register")
    suspend fun registerDevice(@Body request: DeviceRegistrationRequest): RegisterDeviceResponse
}
```

### Sending Notification from Backend

```csharp
public class OrderService
{
    private readonly IMobileServices _mobileServices;

    public async Task ShipOrder(int orderId, long userId)
    {
        // ... ship the order ...

        // Send push notification
        await _mobileServices.SendPushNotificationToUserAsync(
            userId: userId,
            title: "Order Shipped!",
            body: $"Your order #{orderId} is on its way",
            data: new Dictionary<string, string> {
                { "type", "order_shipped" },
                { "orderId", orderId.ToString() },
                { "action", "view_order" }
            }
        );
    }
}
```

## Troubleshooting

### iOS Not Receiving Notifications

1. Check that APNs keys are correctly configured
2. Verify bundle ID matches
3. Ensure certificate is for correct environment (dev/prod)
4. Check device token is being sent correctly

### Android Not Receiving Notifications

1. Verify FCM server key is correct
2. Check that Google Services JSON is in your project
3. Ensure device has Google Play Services
4. Check FCM token is valid

### Web Push Not Working

1. Verify VAPID keys are correctly generated
2. Check service worker is registered
3. Ensure HTTPS is enabled (required for Web Push)
4. Verify subscription object is correctly formatted

### Device Deactivated After Failed Attempts

Devices are automatically deactivated after 5 failed notification attempts. To reactivate:
1. Have the user re-register the device from their mobile app
2. Or manually update `IsActive = true` and reset `FailedAttempts = 0` in database

## Security Considerations

1. **Never expose FCM server keys or APNs private keys in client-side code**
2. **Always use HTTPS** for API endpoints
3. **Validate device tokens** before storing
4. **Implement rate limiting** on registration endpoints
5. **Sanitize notification content** to prevent XSS
6. **Use secure token storage** on mobile devices

## Performance Tips

1. **Batch notifications** when sending to multiple users
2. **Use background jobs** for large notification campaigns
3. **Set appropriate TTL** (time-to-live) for notifications
4. **Monitor failed delivery rates** and clean up inactive tokens
5. **Use topic-based messaging** for broadcast notifications (FCM)

## License

This push notification system is part of the AuthScape platform.
