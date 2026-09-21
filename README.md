# BTPBlazor

A small Blazor web application for SAP BTP that accepts a user message and writes it to the runtime-provided temporary directory.

## Purpose

This app demonstrates a simple pattern for SAP BTP runtime access:

- accept a message from a browser form
- resolve the temp directory from the runtime environment (`TMPDIR`, `TEMP`, `TMP`, or .NET temp path)
- create a unique file in that directory
- show the saved file path back to the user

This is useful for testing how an app can write temporary data in a Cloud Foundry or SAP BTP runtime environment.

## Project structure

- `Program.cs` - app startup and service registration
- `Services/BtpTempFileService.cs` - temp directory resolution and file writing logic
- `Components/Pages/Home.razor` - UI form for entering a message
- `manifest.yml` - SAP BTP deployment manifest
- `BTPBlazor.Tests/TempMessageWriterTests.cs` - tests for the temp writer behavior

## Prerequisites

- .NET 8 SDK
- SAP BTP account with Cloud Foundry entitlement or a compatible target runtime
- Cloud Foundry CLI (`cf`) if deploying to SAP BTP Cloud Foundry

## Run locally

```bash
cd /Users/larry/BTPBlazor
dotnet restore
dotnet run
```

Then open the local URL printed by the app, usually:

```text
http://localhost:5000
```

or

```text
https://localhost:5001
```

## How the temp path is selected

The service resolves the write location in this order:

1. `TMPDIR`
2. `TEMP`
3. `TMP`
4. the .NET default temp path via `Path.GetTempPath()`

This matches the expectation for many BTP runtime containers, where temporary storage is provided via environment variables.

## What the app writes

The app writes a text file with a name similar to:

```text
btp-message-20260918123456789.txt
```

The content is the trimmed message entered by the user.

## Testing

Run the test suite with:

```bash
dotnet test BTPBlazor.Tests/BTPBlazor.Tests.csproj --nologo
```

The tests verify:

- a message is written to the requested temp directory
- an empty or whitespace message is rejected

## Deploying to SAP BTP

### Cloud Foundry deployment

1. Log in to SAP BTP Cloud Foundry:

```bash
cf login
```

2. Push the app from the project root:

```bash
cf push
```

This uses the included `manifest.yml` file.

### Manifest notes

The manifest sets:

```yaml
applications:
  - name: btpblazor
    path: .
    memory: 512M
    stack: cflinuxfs4
    buildpacks:
      - dotnet_core_buildpack
    env:
      ASPNETCORE_ENVIRONMENT: Production
      DOTNET_CLI_TELEMETRY_OPTOUT: "1"
      TMPDIR: /tmp
```

The important SAP BTP runtime setting is `TMPDIR: /tmp`, which aligns with the runtime's temporary directory convention.

## Notes for SAP BTP

- Do not rely on a local workstation path when deployed to BTP.
- Use environment variables or the platform-provided temp path instead.
- Temporary files are appropriate only for short-lived runtime data; they are not a durable storage solution.
- For production workloads, prefer a managed storage service such as SAP HANA, a database, or object storage.

## Security and operational considerations

- The app does not allow arbitrary path traversal; it writes only inside the resolved temp directory.
- File names are generated dynamically to avoid collisions.
- The app is intentionally minimal and suitable as a proof of concept or a starting point for a more complete SAP BTP application.

## Example usage

1. Start the app.
2. Enter: `Hello from SAP BTP!`
3. Submit the form.
4. The app writes the file to the runtime temp directory and displays the file path.

## License

This sample is provided for demonstration purposes.
