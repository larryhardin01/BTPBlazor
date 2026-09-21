# BTPBlazor

A small Blazor web application for SAP BTP that demonstrates writing runtime data to the platform-provided temporary directory, including a chunked streaming write of a large XML payload.

## Purpose

This app demonstrates two related patterns for SAP BTP runtime access:

1. **Simple message write** - accept a message from a browser form and write it to the resolved temp directory.
2. **Chunked large-file streaming** - fetch a 10MB XML payload from an HTTP endpoint and write it to the temp directory 1KB at a time, logging each chunk as it is appended.

This is useful for testing how an app can write temporary data — of varying size and delivery method — in a Cloud Foundry or SAP BTP runtime environment.

## Project structure

- `Program.cs` - app startup, service registration, and the `/getLargeXML` endpoint
- `Services/BtpTempFileService.cs` - temp directory resolution and chunked file-writing logic
- `Components/Pages/Home.razor` - UI for entering a message and triggering the large XML test
- `10MB_Payload.xml` - sample payload served by `/getLargeXML` (included in the build/publish output)
- `Dockerfile` - container image definition used for SAP BTP deployment
- `manifest.yml` - SAP BTP Cloud Foundry deployment manifest
- `BTPBlazor.Tests/TempMessageWriterTests.cs` - tests for the temp writer and chunked copy behavior

## Prerequisites

- .NET 8 SDK
- Docker (for building the deployable container image)
- SAP BTP account with Cloud Foundry entitlement, with Docker-based app support enabled in the target org/space
- Cloud Foundry CLI (`cf`)
- A container registry reachable from the target Cloud Foundry foundation (e.g. GHCR, Docker Hub)

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

## Features

### 1. Write a message to the temp directory

Enter text in the form and click **Write to temp file**. The app writes a text file with a name similar to:

```text
btp-message-20260920123456789.txt
```

The content is the trimmed message entered by the user. The resulting file path is displayed back in the UI.

### 2. Test Large XML (chunked streaming write)

Click **Test Large XML**. The app:

1. Sends a `GET` request to the `/getLargeXML` endpoint, which streams the contents of `10MB_Payload.xml`.
2. Creates an empty target file in the resolved temp directory.
3. Reads the response stream in 1KB chunks and appends each chunk to the target file as it arrives.
4. Logs `Target file: <path>` once the file is created, and `Chunk X appended to file.` as each chunk is written.
5. Closes the file once the entire payload has been written, and displays the final file path.

The chunk log is shown in a scrollable panel. To avoid overwhelming the browser with ~10,000 individual re-renders for a 10MB file, the UI batches its visual refresh every 25 chunks — every chunk is still recorded in the log, just rendered in batches rather than one at a time.

## `/getLargeXML` endpoint

`Program.cs` exposes:

```
GET /getLargeXML
```

This streams `10MB_Payload.xml` from the app's content root as the response body, with range-processing enabled. It exists so the large-file write can be exercised as a real HTTP round trip (download + incremental write) rather than a direct local file copy.

## How the temp path is selected

`BtpTempFileService.ResolveTempDirectory()` resolves the write location in this order:

1. `TMPDIR`
2. `TEMP`
3. `TMP`
4. the .NET default temp path via `Path.GetTempPath()` (which is `/tmp` on Linux)

### Important: environment variables persist on the app in Cloud Foundry

If you set `TMPDIR` (or any env var) via a manifest push, Cloud Foundry stores it as a **user-provided environment variable** on the app itself — this is the same mechanism as `cf set-env`. It persists across subsequent pushes, **even ones that use `--no-manifest`**, until you explicitly remove it:

```bash
cf unset-env btpblazor TMPDIR
cf restart btpblazor   # env var changes require a restart/restage to take effect
```

To see exactly what's configured on a deployed app at any time:

```bash
cf env btpblazor
```

If no `TMPDIR` is set anywhere (manifest, Docker image, or Cloud Foundry container default), the app falls back to `.NET`'s built-in default of `/tmp` on Linux. The app's own UI always shows the actual resolved path after a write, which is the most reliable way to confirm behavior without guessing.

## Testing

Run the test suite with:

```bash
dotnet test BTPBlazor.Tests/BTPBlazor.Tests.csproj --nologo
```

The tests verify:

- a message is written to the requested temp directory
- an empty or whitespace message is rejected
- a small XML payload is copied to the requested temp directory byte-for-byte
- a missing source file throws `FileNotFoundException`
- content spanning multiple 1KB chunks is copied byte-for-byte
- the actual `10MB_Payload.xml` file is copied byte-for-byte (verified via SHA256 hash comparison)

## Deploying to SAP BTP

This app is deployed as a **Docker image**, not via a Cloud Foundry buildpack. Some SAP BTP Cloud Foundry foundations do not have a .NET buildpack available (`cf buildpacks` will show only things like Java, Node, Go, Python, PHP, etc. in that case), so the Docker image path is the most portable option.

### 1. Build the image for the correct architecture

Cloud Foundry cells commonly run `linux/amd64`. If you're building on Apple Silicon, you must target that platform explicitly or the image will fail to stage with an architecture mismatch error:

```bash
cd /Users/larry/BTPBlazor
docker buildx build --platform linux/amd64 -t ghcr.io/<your-github-user>/btpblazor:latest --push .
```

This builds and pushes to GHCR (GitHub Container Registry) in one step. Make sure the GHCR package visibility is set to **Public**, or Cloud Foundry will fail to pull it with an authentication error during staging.

### 2. Push the app as a Docker image

```bash
cf login
cf target -o <your-org> -s <your-space>
cf push btpblazor --docker-image ghcr.io/<your-github-user>/btpblazor:latest --no-manifest
```

`--no-manifest` is required if the app was previously pushed as a buildpack-lifecycle app, since Cloud Foundry will not let you apply `docker:` settings on top of an existing buildpack-lifecycle app definition. If you get a "Docker cannot be configured for a buildpack lifecycle app" error, delete the old app first:

```bash
cf delete btpblazor -f
cf push btpblazor --docker-image ghcr.io/<your-github-user>/btpblazor:latest --no-manifest
```

### Manifest notes

`manifest.yml` is kept for reference and for buildpack-based deployments where a .NET buildpack is available:

```yaml
applications:
  - name: btpblazor
    memory: 512M
    stack: cflinuxfs4
    env:
      ASPNETCORE_ENVIRONMENT: Production
      DOTNET_CLI_TELEMETRY_OPTOUT: "1"
      TMPDIR: /tmp
```

Note: if you push with the Docker image flow, this manifest is not applied (`--no-manifest`), so `TMPDIR` and other values here will not take effect unless you've previously set them directly on the app (see the environment variable persistence note above).

### Dockerfile

`Dockerfile` builds and publishes the app using the .NET 8 SDK, then copies the published output into an ASP.NET Core runtime image, listening on port `8080`.

## Notes for SAP BTP

- Do not rely on a local workstation path when deployed to BTP.
- Use environment variables or the platform-provided temp path instead.
- Temporary files are appropriate only for short-lived runtime data; they are not a durable storage solution.
- For production workloads, prefer a managed storage service such as SAP HANA, a database, or object storage.

## Security and operational considerations

- The app does not allow arbitrary path traversal; it writes only inside the resolved temp directory.
- File names are generated dynamically to avoid collisions.
- The `/getLargeXML` endpoint only serves the single bundled sample payload; it does not accept a path parameter and cannot be used to read arbitrary files.
- The app is intentionally minimal and suitable as a proof of concept or a starting point for a more complete SAP BTP application.

## Example usage

1. Start the app.
2. Enter: `Hello from SAP BTP!`
3. Submit the form. The app writes the file to the runtime temp directory and displays the file path.
4. Click **Test Large XML**. The app streams the 10MB payload from `/getLargeXML`, writes it in 1KB chunks, logs each chunk, and displays the final file path.

## License

This sample is provided for demonstration purposes.


## License

This sample is provided for demonstration purposes.
