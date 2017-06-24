package com.microsoft.speech.bankdemo;

import android.content.Intent;
import android.media.AudioFormat;
import android.media.AudioManager;
import android.media.AudioTrack;
import android.os.AsyncTask;
import android.os.Handler;
import android.os.Message;

import android.os.Bundle;
import android.support.v7.app.AlertDialog;
import android.support.v7.app.AppCompatActivity;
import android.view.LayoutInflater;
import android.view.View;
import android.widget.EditText;
import android.widget.ImageButton;
import android.widget.TextView;
import android.widget.Toast;

import com.microsoft.bing.speech.SpeechClientStatus;
import com.microsoft.cognitiveservices.speechrecognition.ISpeechRecognitionServerEvents;
import com.microsoft.cognitiveservices.speechrecognition.MicrophoneRecognitionClient;
import com.microsoft.cognitiveservices.speechrecognition.RecognitionResult;
import com.microsoft.cognitiveservices.speechrecognition.SpeechRecognitionMode;
import com.microsoft.cognitiveservices.speechrecognition.SpeechRecognitionServiceFactory;
import com.microsoft.speech.ttsclientsdk.ITTSCallback;
import com.microsoft.speech.ttsclientsdk.Synthesizer;

import java.util.Date;

public class BDHomepage extends AppCompatActivity implements ISpeechRecognitionServerEvents{
    private ImageButton talkButton;
    private ImageButton transferButton;
    private ImageButton accountinfoButton;
    private ImageButton paymentButton;
    private ImageButton billButton;
    private EditText talkLable;

    private MicrophoneRecognitionClient micClient = null;

    private Synthesizer m_syn;
    private AudioTrack audioTrack;
    private final int SAMPLE_RATE = 16000;
    private long handle = 0;
    private int result= 0 ;
    private boolean isRecoding;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_bdhomepage);

        talkLable = (EditText)findViewById(R.id.talkLable);
        talkButton = (ImageButton)findViewById(R.id.talkButton);
        transferButton = (ImageButton)findViewById(R.id.transferButton);
        accountinfoButton = (ImageButton)findViewById(R.id.accountButton);
        paymentButton = (ImageButton)findViewById(R.id.paymentButton);
        billButton = (ImageButton)findViewById(R.id.billButton);

        if (getString(R.string.primaryKey).startsWith("Please")) {
            new android.app.AlertDialog.Builder(this)
                    .setTitle(getString(R.string.add_subscription_key_tip_title))
                    .setMessage(getString(R.string.add_subscription_key_tip))
                    .setCancelable(false)
                    .show();
        }

        isRecoding = false;
        initAudioTrack();
        initSynthesizer();
        initSR();

        talkButton.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                if(isRecoding){
                    isRecoding = false;
                    talkLable.setText("");
                    micClient.endMicAndRecognition();
                }else{
                    isRecoding = true;
                    talkLable.setText("");
                    micClient.startMicAndRecognition();
                }
            }
        });

        transferButton.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                Intent intent=new Intent(BDHomepage.this,BDTransfer.class);
                startActivity(intent);
            }
        });

        accountinfoButton.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                Intent intent=new Intent(BDHomepage.this,BDAccountinfo.class);
                startActivity(intent);
            }
        });

        paymentButton.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                Toast.makeText(getApplicationContext(), "功能暂未开启", Toast.LENGTH_SHORT).show();
            }
        });

        billButton.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                Toast.makeText(getApplicationContext(), "功能暂未开启", Toast.LENGTH_SHORT).show();
            }
        });
    }

    @Override
    protected void onDestroy() {
        destroyAudioTrack();
        destroySynthesizer();
        super.onDestroy();
    }

    //region TTS
    private void initAudioTrack(){
        //init audioTrack
        audioTrack = new AudioTrack(AudioManager.STREAM_MUSIC, SAMPLE_RATE, AudioFormat.CHANNEL_CONFIGURATION_MONO,
                AudioFormat.ENCODING_PCM_16BIT, AudioTrack.getMinBufferSize(SAMPLE_RATE, AudioFormat.CHANNEL_CONFIGURATION_MONO, AudioFormat.ENCODING_PCM_16BIT), AudioTrack.MODE_STREAM);
        audioTrack.play();
    }

    private void destroyAudioTrack(){
        audioTrack.flush();
        audioTrack.stop();
        audioTrack.release();
    }

    private void initSynthesizer(){
        //init Synthesizer
        m_syn = new Synthesizer();
        //set Synthesizer call back
        m_syn.setReceiveWave(new ITTSCallback() {
            public int ReceiveWave(Object callBackStat, final byte[] data, int size) {
                //write to audio card
                audioTrack.write(data, 0, size);
                return 0;
            }
        });

        //init speech handle
        handle = m_syn.MSTTS_CreateSpeechSynthesizerHandler(handle, getResources().getString(R.string.primaryKey));

        if(handle == -1){
            talkLable.setText("initialization failed.");
            result = -1;
        }

        //set output
        if(result == 0){
            result = m_syn.MSTTS_SetOutput(handle, null, m_syn);
        }
    }

    private void destroySynthesizer() {
        m_syn.MSTTS_CloseSynthesizer(handle);
    }
    //endregion

    //region SpeechReco implement ISpeechRecognitionServerEvents
    private void initSR() {
        if (this.micClient == null) {
            this.micClient = SpeechRecognitionServiceFactory.createMicrophoneClient(
                    this,
                    SpeechRecognitionMode.LongDictation,
                    this.getString(R.string.speech_language),
                    this,
                    this.getString(R.string.primaryKey));
            this.micClient.setAuthenticationUri(this.getString(R.string.authenticationUri));
        }
    }

    public void onFinalResponseReceived(final RecognitionResult response) {
        if(isRecoding){
            isRecoding = false;
            this.micClient.endMicAndRecognition();

            if(response.Results.length > 0) {
                talkLable.setText(response.Results[0].DisplayText);
            }

            String data = talkLable.getText().toString().trim();
            if((data.indexOf("多少") != -1) || (data.indexOf("查询") != -1) || (data.indexOf("余额") != -1)){
                AccountInfoDialog();
                AsyncTask.execute(new Runnable() {
                    @Override
                    public void run() {
                        m_syn.MSTTS_SetOutput(handle, null, m_syn);
                        m_syn.MSTTS_Speak(handle, "您的账户余额为：1458485.45元", 0);
                    }
                });
            }else if((data.indexOf("转账") != -1) || (data.indexOf("转帐") != -1)){
                TransferDialog(data);
                AsyncTask.execute(new Runnable() {
                    @Override
                    public void run() {
                        m_syn.MSTTS_SetOutput(handle, null, m_syn);
                        m_syn.MSTTS_Speak(handle, "已填好您的转账请求，请确认", 0);
                    }
                });
            }else{
                AsyncTask.execute(new Runnable() {
                    @Override
                    public void run() {
                        m_syn.MSTTS_SetOutput(handle, null, m_syn);
                        m_syn.MSTTS_Speak(handle, "对不起，我不知道你在说什么", 0);
                    }
                });
            }
        }
    }

    public void onPartialResponseReceived(final String response) {
        talkLable.setText(response);
    }

    public void onIntentReceived(final String payload) {
        talkLable.setText(payload);
    }

    public void onError(final int errorCode, final String response) {
        talkLable.setText("Error code: " + SpeechClientStatus.fromInt(errorCode) + " " + errorCode + "\r\nError text: " + response);
    }

    public void onAudioEvent(boolean recording) {
        if (recording) {
            talkLable.setText("Please start speaking.");
        }

        if (!recording) {
            this.micClient.endMicAndRecognition();
        }
    }
    //endregion

    //region interface design
    public void AccountInfoDialog(){
        new  AlertDialog.Builder(this)
                .setTitle("账户余额" )
                .setMessage("您的账户余额为：1458485.45元" )
                .setPositiveButton("确定" ,  null )
                .show();
    }

    public void TransferDialog (String data) {
        LayoutInflater layoutInflater = LayoutInflater.from(this);
        View trsndgrtDialogView = layoutInflater.inflate(R.layout.transfer, null);

        EditText mUserName = (EditText)trsndgrtDialogView.findViewById(R.id.edit_username);
        EditText mPassword = (EditText)trsndgrtDialogView.findViewById(R.id.edit_password);

        String[] name = data.split("给|转帐");

        if(name.length > 2){
            mUserName.setText(name[1]);
            mPassword.setText(String.valueOf(NumberFormatUtil.convert(name[2])));
        }else{
            name = data.split("给|转账");
            if(name.length > 2){
                mUserName.setText(name[1]);
                mPassword.setText(String.valueOf(NumberFormatUtil.convert(name[2])));
            }
        }

        AlertDialog longinDialog = new AlertDialog.Builder(this)
                .setTitle("转账")
                .setView(trsndgrtDialogView)
                .setPositiveButton("确定", null)
                .setNeutralButton("取消", null)
                .create();
        longinDialog.show();
    }
    //endregion
}
