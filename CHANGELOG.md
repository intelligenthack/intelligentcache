# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [4.0.0] - 2025-01-29

### Breaking Changes

- **Dropped support for .NET 6.0 and .NET 7.0**. The library now targets .NET 8.0 and .NET 9.0.
- **StackExchange.Redis upgraded to 2.10.1**. If you pass `RedisChannel` values directly, you must now use `RedisChannel.Literal()` or `RedisChannel.Pattern()` instead of implicit string conversion.

### Changed

- Updated `StackExchange.Redis` from 2.0.495 to 2.10.1
- Updated `System.Runtime.Caching` from 4.7.0 to 8.0.1
- Added explicit reference to `System.Security.Cryptography.Xml` 8.0.2

### Security

- Fixed **Critical** vulnerability in `System.Drawing.Common` 4.7.0 ([GHSA-rxg9-xrhp-64gj](https://github.com/advisories/GHSA-rxg9-xrhp-64gj))
- Fixed **High** vulnerability in `System.IO.Pipelines` 4.5.0 ([GHSA-j378-6mmw-hqfr](https://github.com/advisories/GHSA-j378-6mmw-hqfr))
- Fixed **Moderate** vulnerability in `System.Security.Cryptography.Xml` 4.5.0 ([GHSA-vh55-786g-wjwj](https://github.com/advisories/GHSA-vh55-786g-wjwj))

### Migration Guide

#### Target Framework

Update your project to target .NET 8.0 or later:

```xml
<TargetFramework>net8.0</TargetFramework>
```

#### RedisChannel API

If you're using `RedisInvalidationSender` or `RedisInvalidationReceiver` with a string channel name, update to use the explicit `RedisChannel.Literal()` method:

```csharp
// Before (v3.x)
var sender = new RedisInvalidationSender(subscriber, "my-channel");

// After (v4.0)
var sender = new RedisInvalidationSender(subscriber, RedisChannel.Literal("my-channel"));
```

## [3.3] and earlier

See [GitHub Releases](https://github.com/intelligenthack/intelligentcache/releases) for previous versions.
