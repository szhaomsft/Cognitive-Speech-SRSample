package com.microsoft.cognitivesample;

import java.io.FileInputStream;
import java.net.URI;
import java.util.Scanner;

import com.microsoft.bing.speech.Preferences;
import com.microsoft.bing.speech.RecognitionPartialResult;
import com.microsoft.bing.speech.RecognitionResult;
import com.microsoft.bing.speech.SpeechClient;
import com.microsoft.bing.speech.SpeechInput;

public class BingSpeechJavaSDKSample {

	public static void main(String[] args) {
		try{
			String speechLanguage = "en-US";
			URI uri = URI.create("wss://websockets.platform.bing.com/ws/speech/recognize");
		    CognitiveServicesAuthorizationProvider authorizationProvider = new CognitiveServicesAuthorizationProvider("Your Subscription ID");
		    Preferences preferences = new Preferences(speechLanguage, uri, authorizationProvider, true);
		    SpeechClient speechClient = new SpeechClient(preferences);
		    
		    //Register the event handler to handle partial recognition result or error
		    speechClient.subscribeToRecognitionPartialResult((rbr) -> {
		    	System.out.println("--- Partial result ---");
		    	System.out.println(new String(((RecognitionPartialResult)rbr).displayText));
		    	System.out.println();
		    });
		    //Register the event handler to handle recognition result or error
		    speechClient.subscribeToRecognitionResult((rbr) -> {
		    	System.out.println();
		    	System.out.println("--- Recognition Phrase result ---");
		    	System.out.println(new String(((RecognitionResult)rbr).phrases.get(0).displayText));
		    });
		    
		    FileInputStream fis = new FileInputStream("whatstheweatherlike.wav");
		    SpeechInput speechInput = new SpeechInput(fis, null);
		    speechClient.recognize(speechInput);
		  
		    Scanner sc = new Scanner(System.in);
	        System.out.println("Press any key to exit...");
	        while(sc.hasNextLine()) System.exit(0);;
		}catch(Exception ex){
			ex.printStackTrace();
		}
	}

}
