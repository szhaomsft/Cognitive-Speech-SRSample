# SpeechBot

Here is a quick guide on how to build, run and publish this SpeechBot demo.

## Prerequisites

1. Azure SDK
1. NodeJS (https://nodejs.org/en/)
1. Webpack (`npm install -g webpack`)

## Getting started

### How to build the web page (developement)

Goto `SpeechBot\view` and run `npm install` then `webpack`. Optionally, you can run `webpack --watch` to enable incremental build.

### How to build the web page (retail)

Goto `SpeechBot\view` and run `npm install` (if not previsouly run) then run `webpack -p`.

### How to verify if web page is correctly built

You should see `app.js` and `<hashcode>.ttf` under `SpeechBot\wwwroot\assets`.

### How to build the backend

1. First locate your Cognitive Service Bing Speech API key.
2. (Optional) Fill in the `srKey` and `ttsKey` slots in `SpeechBot\Startup.cs`. 
3. Open SpeechBot.sln and build for x64.

### Run it locally

Open the solution with Visual Studio and build the solution. Then use F5 to start debugging or Ctrl+F5 to start without debugging. When the browser prompts to switch to https protocal, **click cancel**.

### How to deploy to Azure App Service

Publish to folder and then FTP copy to /site/wwwroot.

Clear cache of browser before reloading.