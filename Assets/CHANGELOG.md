# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [5.27.1] - 2026-02-17
### Added
- Vivox clients will now reconnect automatically when Vivox servers make live upgrades.
### Changed
- Windows: Now only audio input devices have the `AudioCategory_GameChat` AudioCategory specified by default due to Windows forcing some Bluetooth devices into a low quality mode when `AudioCategory_GameChat` was specified for an audio output device. Use `vx_sdk_config_t.capture_audio_stream_category` to change the capture audio stream category at initialization.
### Fixed
- Android: Building the SDKSampleApp from source is now achievable in modern Android Studio versions without major build file changes.

## [5.27.0] - 2025-11-17
### Added
- Added support for Nintendo Switch™ 2.
### Changed
- Windows: Audio devices opened by the Vivox SDK have their AudioCategory marked as `AudioCategory_GameChat` for client apps that have declared compatibility with Windows 10 and 11 in their manifest.
### Fixed
- Android armeabi/armeabi-v7a: Fixed crash that would occur on certain SoCs at the time of Vivox login when custom allocators were not specified.
- Fixed layered service provider debug log spam from Automatic Connection Recovery (ACR).
- All time-dependent operations within the Vivox SDK are now immune to system clock changes.

## [5.26.1] - 2025-05-02
### Fixed
- Fixed a bug where it would not be possible to join channels on Nintendo Switch™ when running in compatibility mode.

## [5.26.0] - 2025-03-31
### Added
- Noise suppression: Now applied to capture audio by default. To change the settings, use `vx_get_noise_suppression_enabled`, `vx_get_noise_suppression_level`, `vx_set_noise_suppression_enabled`, and `vx_set_noise_suppression_level`.
- Vivox Acoustic Echo Cancellation (AEC) and Automatic Gain Control (AGC) are now available on all platforms. AEC and AGC were previously unavailable on Nintendo Switch™, PlayStation®, Xbox, visionOS, and UWP platforms.
- New audio callback `on_audio_unit_requesting_final_mix_for_echo_canceller_analysis`. Mix application audio into the provided buffer so Vivox acoustic echo cancellation can attempt to remove it from microphone capture signals.
- Android: Rebuilt the Vivox native libraries with 16KB page size support.
### Changed
- Vivox Acoustic Echo Cancellation (AEC) and Automatic Gain Control (AGC) have had their underlying algorithms replaced.
    - AEC on mobile platforms no longer mutes the microphone signal when echo is detected. Instead, echo is removed and speech continues to transmit.
    - AGC is on by default. The new AGC increases microphone capture loudness on all platforms. Consider adjusting application volume presets if they were changed from the default volumes; perhaps going so far as to reset user volume settings entirely or to adjust them by some formula.
    - For platforms that already had AEC and AGC, the new implementation including the addition of Noise Suppression has:
        - The same or less CPU cost than the previous implementation when using the Release configuration.
        - A reduction in memory usage of about 11 MB while a voice session is active.
    - For platforms that are getting AEC and AGC for the first time:
        - CPU cost of the Vivox audio thread increases by about 44% when using the Release configuration.
        - An extra 6 MB of memory will be used while a voice session is active.
    - Vivox library sizes have increased for all platforms due to the change in audio processing implementations:
        - Release configuration libraries add between 53 KB and 8 MB more to an application than the previous Vivox release.
        - Debug configuration libraries can add upwards of 12 MB more to an application than the previous Vivox release.
- Network optimization: A new default voice codec bitrate was chosen to reduce packet loss and jitter. The new setting was found to be indistinguishable from the previous setting in structured listening tests.
- Android and iOS: `vx_set_platform_aec_enabled` now functions whether or not Dynamic Voice Processing Switching is enabled. DVPS will adhere to the most recent setting during operation.
- iOS: The `ios_voice_processing_io_mode` value now defaults to 1 rather than 2, meaning that the VoiceProcessingIO (VPIO) unit will only be used when the speakerphone is used for render and capture. This change favors the use of the new AEC underlying algorithms rather than the use of the VPIO unit to avoid the decrease in quality for most audio device configurations that come from using the VPIO unit.
- The licenses and third party notices have been updated.
### Removed
- `iOS`:
    - Removed bitcode from the iOS libraries.
    - Removed support for ARMv7 architecture on iOS platforms.

### Fixed
- Numerous areas of the SDK were brought into compliance with Vivox custom memory allocators. Expect more activity with the custom memory functions. Consider adjusting memory pool limits if any were previously in place. Vivox's own memory pool now further reduces the number of requests for heap memory when custom memory allocators are not set.
- A rare Vivox threads deadlock that could leave Vivox unresponsive after removing a session.
- Android: Vivox's Proguard rules are now included in the AAR libraries so that minification does not require adding rules manually to prevent a crash.
- Vivox log levels were off-by-one. There may be more Vivox log statements now if `initial_log_level` was set for your application.
### Known Issues
- Android: Although the Vivox native libraries support 16KB page size, the Vivox sample applications don't have 16KB page size support yet.

## [5.25.4] - 2024-12-18
### Changed
- The licenses and third party notices have been updated.
- Gamecore: Improved handling of hotswapping capture and render devices.
### Fixed
- Gamecore: Issues preventing Remote Control functioning with Vivox through Xbox Manager. See documentation for full details.
- Gamecore: A bug where USB audio devices were not being detected when plugged in.

## [5.25.2] - 2024-09-16
### Fixed
- A crash that would occur on visionOS when joining a channel.

## [5.25.1] - 2024-08-27
### Changed
- PlayStation® libraries are now provided as dynamic libraries.

## [5.25.0] - 2024-08-23
### Added
- API added for users to give and revoke consent for SafeVoice services. SafeVoice consent can also be queried.
### Changed
- Android: Mobile recording conflicts avoidance is now disabled by default.
### Fixed
- A bug where disconnecting the currently specified capture or render device and reconnecting it again would result in no audio.
- A bug causing thread accumulation with each subsequent connector creation, login and connector shutdown.
- A bug which caused voice to not work on GDK when using the debug libraries

## [5.24.0] - 2024-07-08
### Added
- A new `vx_sdk_config` member `ios_voice_processing_io_mode` to configure the Audio Unit used on iOS. The default mode is 2, which will always use the VoiceProcessingIO (VPIO) unit to guarantee voice chat quality. Setting this to 1 will only use the VPIO unit while using the speakerphone, other routes will use the RemoteIO audio unit. Setting it to 0 (not recommended) will never use the VPIO audio unit. It is also possible to use the `vx_get_ios_voice_processing_io_mode` to get the current value of this setting and `vx_set_ios_voice_processing_io_mode` to set it after initialization. Setting it at runtime will force a reconfiguration of the audio settings to apply the change.
- New fields to the Conversation API, `unread_count` and `display_name`. In the `vx_conversation` struct, these fields represent the number of unread messages
since the last marker was set for a specific conversation. The display name is included if the conversation is a direct messaging conversation.
### Changed
- Automatic Connection Recovery (ACR) detection of network reachability is now more stable.
### Fixed
- Improved iOS handling of capture devices. When Vivox detects that the device seems to be invalidated, it will close and reopen the device to recover.
- All Microsoft platforms will now attempt to reset capture and render devices if they become invalidated.
- A bug where the block state was not correct when a block or unblock operation was performed while a user that was in the channel previously is not currently in the channel.
- A bug where an ACR connection recovery would cause an RTP timeout after some time.
- A bug where rejoining a 3D channel while also in another channel would lead to no participant update events for participants that were seen before leaving the 3D channel.
- A memory leak in a log function when RTP channels fail to connect
- A potential race condition with RTP teardown
- A bug where `vx_control_communications_operation_block_list` would return duplicates in the list of blocked users.

## [5.23.1] - 2024-04-18
### Added
- `macOS`, `iOS`, `visionOS`:
    - Added the new PrivacyInfo.xcprivacy file. It is required in your Xcode project when generating your application's privacy report before submitting it to the App Store. For more details, refer to this page of the Unity documentation: <https://docs.unity.com/ugs/en-us/manual/vivox-unity/manual/Unity/privacy/apple-privacy-manifest>
### Changed
- `VxcTypes.h`:
    - Renamed `mobile_recording_conflicts_avoidance`  to `vx_sdk_config`
- `Android`:
    - The `vx_sdk_config` field `mobile_recording_conflicts_avoidance` can now be used to enable or disable the recording conflicts avoidance. Enabled by default, this can be disabled so that Vivox does not try to recover if another application is recording at the same time as Vivox.
- `iOS`:
    - The `vx_sdk_config`'s field `bluetooth_profile` can now be used to configure whether the AVAudioSession uses A2DP or HFP Bluetooth profiles.
### Known Issues
- If the SDK is initialized before an XBox user is logged in on the console, controller events do not come through.
- Starting multiple clients quickly can cause Windows to lose DNS resolution ability.

## [5.23.0] - 2024-03-06
### Added
- Support for Apple Silicon devices.
- Support for visionOS along with the visionOS simulator (Experimental).
- Support for iOS simulator for local testing.
- A new API for managing volume-based audio duplication suppression. By default, the SDK renders audio only once for a remote participant transmitting to multiple channels, specifically where the participant's volume is loudest.
- A new API for controlling 3D channel volume protection. When enabled, this feature reduces the maximum loudness of 3D channels to prevent distortion when sound is off-center. Disabling it increases the maximum loudness, but may result in slight distortion at default volume levels.
- A new API for handling audio clipping protection. This feature only affects boosted audio (capture or render with volume settings greater than 50). When enabled, it allows Vivox to reduce the dynamic range of samples that are near clipping. When disabled, it allows ordinary clipping to occur.
- A new API for adjusting the parameters of the audio clipping protector. This allows for customization of the audio clipping protector's behavior.
- `Vxc.h`:
    - Added `vx_set_volume_based_duplication_suppression_enabled`: This method allows you to enable or disable volume-based audio duplication suppression.
    - Added `vx_get_volume_based_duplication_suppression_enabled`: This method allows you to get the internal volume-based audio duplication suppression state.
    - Added `vx_set_3d_channel_volume_protection_enabled`: This method allows you to enable or disable the SDK's internal volume protection for 3D channels.
    - Added `vx_get_3d_channel_volume_protection_enabled`: This method allows you to get the internal 3D channel volume protection state.
    - Added `vx_set_audio_clipping_protector_enabled`: This method allows you to enable or disable the SDK's internal audio clipping protector (soft clipper).
    - Added `vx_get_audio_clipping_protector_enabled`: This method allows you to get the internal audio clipping protector enabled state.
    - Added `vx_set_audio_clipping_protector_parameters`: This method allows you to change the behavior of the SDK's internal audio clipping protector (soft clipper).
    - Added `vx_get_audio_clipping_protector_parameters`: This method allows you to get the internal audio clipping protector's parameters.
- `Android`:
    - A Bluetooth Low Energy API making for smoother Bluetooth connections on newer Android versions.
    - A new way to disable mono downmixing on speakerphone by adding an Android meta-data name-value pair to your application's AndroidManifest.xml.
### Changed
- Reduced the amount of memory used by the Automatic Connection Recovery.
- `VxcTypes.h`:
    - Renamed `pf_on_audio_unit_before_recv_audio_mixed_t` parameter `initial_target_uri` to `session_uri` to better reflect its usage.
- `VxcRequests.h`:
    - Undeprecated `vx_req_account_set_login_properties`.
- `Android`:
    - On Meta/Oculus devices, audio is now always rendered in stereo when using the speakerphone.
- `iOS`:
    - Restored bitcode in the iOS libraries.
### Fixed
- An issue with the `pf_on_audio_unit_before_recv_audio_mixed_t` audio callback where only the audio of a single channel was provided when a participant was transmitting to multiple channels.
- An issue where the `pf_on_audio_unit_before_recv_audio_mixed_t` audio callback would not get triggered for a participant in the second channel of a session group.
- `Android`:
    - Fixed a bug where all audio would route to speakerphone when the Bluetooth permissions were granted after having joined a channel.
- `macOS`:
    - Fixed an issue where captured sounds would be robotic on some macOS devices.
- `PlayStation®`:
    - Fixed a deadlock on PlayStation® when Automatic Connection Recovery would be triggered.
### Known Issues
- If the SDK is initialized before an XBox user is logged in on the console, controller events do not come through.
- Starting multiple clients quickly can cause Windows to lose DNS resolution ability.
- Starting multiple clients in quick succession may cause Windows 11 to lose its ability to resolve DNS.