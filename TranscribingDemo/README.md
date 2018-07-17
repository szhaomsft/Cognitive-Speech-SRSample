# Transcribing Demo

Here is a quick guide on how to build, run and publish this transcribing demo.

## Prerequisites

1. .Net Framework >= 4.6.1
1. NodeJS (https://nodejs.org/en/)
1. Webpack (`npm install -g webpack`)  
    For webpack 4.0.x, you also need to install webpack-cli (`npm install -g webpack-cli`)

## Getting started

### How to build the web page (development)

Goto `SpeechBot\view` and run `npm install` then `webpack`. Optionally, you can run `webpack --watch` to enable incremental build.

### How to build the web page (retail)

Goto `SpeechBot\view` and run `npm install` (if not previously run) then run `webpack -p`.

### How to verify if web page is correctly built

<!-- You should see `app.js` and `<hashcode>.ttf` under `SpeechBot\wwwroot\assets`. -->
You should see `app.js` under `SpeechBot\wwwroot\assets`.

### How to build the backend

1. First locate your [Unified Speech Services](https://docs.microsoft.com/en-us/azure/cognitive-services/speech-service/overview) API key.  
    - Get api key for [free](https://azure.microsoft.com/en-us/try/cognitive-services/?api=speech-services) or [paid](https://go.microsoft.com/fwlink/?LinkId=872236&clcid=0x409).
2. (Optional) Fill in the `UnifiedKey`, `UnifiedRegion` and `Locale` slots in `SpeechBot\Startup.cs`.
3. Open `SpeechBot.sln` and build for x64.  
    - Internet access is required in the first run to restore `NuGET` packages.

### Run it locally

Open the solution with Visual Studio and build the solution. Then use `F5` to start debugging or `Ctrl+F5` to start without debugging. When the browser prompts to switch to https protocol, click **cancel**.

Notes:

- It can be run on remote server. Make sure server 2016 has remote audio turned on (Need to install remote desktop service)
- The speechbot.exe is run as a webserver. More details can find [here](http://mikko.repolainen.fi/documents/aspdotnet-core-hosting).

### How to deploy to Azure App Service or Other Windows Server

- Check if .Net Framework 4.6.1 is installed. If not, install it.
- Make sure [Microsoft Visual C++ Redistributable for Visual Studio 2017](https://support.microsoft.com/en-us/help/2977003/the-latest-supported-visual-c-downloads) is on the system. The `x64` version could be downloaded from [here](https://aka.ms/vs/15/release/vc_redist.x64.exe). (See [here](https://docs.microsoft.com/en-us/azure/cognitive-services/speech-service/ship-application) for details.)
- Publish to folder and then FTP copy to `/site/wwwroot`.
- Clear cache of browser before reloading.
