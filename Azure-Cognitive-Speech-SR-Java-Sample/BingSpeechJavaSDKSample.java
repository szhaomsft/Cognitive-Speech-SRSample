package com.microsoft.cognitivesample;

import java.io.FileInputStream;
import java.net.URI;

import com.microsoft.bing.speech.Preferences;
import com.microsoft.bing.speech.SpeechClient;
import com.microsoft.bing.speech.SpeechInput;

public class BingSpeechJavaSDKSample {

	public static void main(String[] args) {
		try{
			String speechLanguage = "en-US";
			URI uri = URI.create("wss://websockets.platform.bing.com/ws/speech/recognize");
		    CognitiveServicesAuthorizationProvider authorizationProvider = new CognitiveServicesAuthorizationProvider("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx");
		    Preferences preferences = new Preferences(speechLanguage, uri, authorizationProvider, true);
		    SpeechClient speechClient = new SpeechClient(preferences);
		    
		    speechClient.subscribeToRecognitionResult((rr) -> {
		    	System.out.println(new String(rr.phrases.get(0).displayText));
		    });
		    
		    FileInputStream fis = new FileInputStream("D:/WorkSpace/Temp/MediaSamples/speech/mine.wav");
		    SpeechInput speechInput = new SpeechInput(fis, null);
		    speechClient.recognize(speechInput);
		    
		    Thread.currentThread().sleep(15000);
		}catch(Exception ex){
			ex.printStackTrace();
		}
	}

}
