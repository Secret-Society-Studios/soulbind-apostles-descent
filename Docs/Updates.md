# In-game updates (Android, sideloaded)

On launch the game reads `version.json` from the newest GitHub release. If that build number is
higher than the installed one, it offers to download the APK, checks its hash and opens Android's
install screen. Code: `Assets/Scripts/Managers/GameUpdater.cs`.

## One-time setup: the signing key

Android only installs an update signed with the same key as the installed game, so every build must
use one shared keystore. Do this once, on one PC:

```
keytool -genkeypair -v -keystore soulbind.keystore -alias soulbind -keyalg RSA -keysize 2048 -validity 10000
```

Keep the keystore and its passwords outside the repo, in a private place the team shares. If it is
lost, players can never update again without uninstalling. Never commit it: this repo is public.

Set these on every PC that builds (Windows: System > Environment Variables, then restart Unity):

| Variable | Value |
| --- | --- |
| `SOULBIND_KEYSTORE` | Full path to `soulbind.keystore` |
| `SOULBIND_KEYSTORE_PASS` | The keystore's password |
| `SOULBIND_KEY_ALIAS` | `soulbind` |
| `SOULBIND_KEY_PASS` | The key's password |

Without them, SoulBind > Build Android APK warns and signs with this PC's debug key. Don't hand out
that build.

## Publishing a build

1. Run **SoulBind > Build Android APK...** It writes `SoulBind-vX.Y.Z.apk` and `version.json` to `Builds/Android`.
2. Commit `ProjectSettings/ProjectSettings.asset`, so the team continues from the same build number.
3. Create a GitHub release and attach **both** files. Don't tick "pre-release" or "draft": the game
   reads the release marked Latest.

Players see the update the next time they start the game.

## Things to know

- Apps installed with a different key can't be updated, so the first build with the new keystore
  needs a manual uninstall and install, which also deletes that phone's saved data.
- Builds from before the updater was added need one manual install.
- Keep the package name the same: a new one installs as a separate game.
- Google Play doesn't allow this. Remove the updater and the `SoulBindUpdater.androidlib` plugin
  before publishing there, and use Google's In-App Updates instead.
