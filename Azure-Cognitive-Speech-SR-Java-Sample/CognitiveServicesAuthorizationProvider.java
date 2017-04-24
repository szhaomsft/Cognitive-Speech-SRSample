package com.microsoft.cognitivesample;

import org.apache.http.HttpEntity;
import org.apache.http.HttpResponse;
import org.apache.http.client.HttpClient;
import org.apache.http.client.methods.HttpPost;
import org.apache.http.impl.client.HttpClients;
import org.apache.http.util.EntityUtils;

import com.microsoft.bing.speech.IAuthorizationProvider;

public class CognitiveServicesAuthorizationProvider implements IAuthorizationProvider {

	private String fetchTokenUri = "https://api.cognitive.microsoft.com/sts/v1.0/issueToken";
    private String subscriptionKey;

    public CognitiveServicesAuthorizationProvider(String subscriptionKey) throws Exception {
        if (subscriptionKey == null) {
            throw new Exception("subscriptionKey is null");
        }

        this.subscriptionKey = subscriptionKey;
    }
	
	public String GetAuthorizationToken(){
		return fetchToken(fetchTokenUri, this.subscriptionKey);
	}
	
    private String fetchToken(String fetchUri, String subscriptionKey) {
    	String token = null;
    	
		try {
			HttpClient httpclient = HttpClients.createDefault();
			String url = "https://api.cognitive.microsoft.com/sts/v1.0/issueToken";
			HttpPost authRequest = new HttpPost(url);
			authRequest.addHeader("Content-type", "application/x-www-form-urlencoded");
			authRequest.addHeader("Ocp-Apim-Subscription-Key", subscriptionKey);
			HttpResponse authResponse = httpclient.execute(authRequest);
			HttpEntity authEntity = authResponse.getEntity();
			token = EntityUtils.toString(authEntity);
		} catch (Exception ex) {
			ex.printStackTrace();
		}
		
		return token;
     }
}
