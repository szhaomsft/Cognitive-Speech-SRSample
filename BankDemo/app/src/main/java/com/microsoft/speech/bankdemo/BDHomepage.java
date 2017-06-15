package com.microsoft.speech.bankdemo;

import android.content.Intent;
import android.media.AudioFormat;
import android.media.AudioManager;
import android.media.AudioRecord;
import android.media.AudioTrack;
import android.media.MediaRecorder;
import android.os.AsyncTask;
import android.os.Handler;
import android.os.Message;
import android.support.v7.app.AlertDialog;
import android.support.v7.app.AppCompatActivity;
import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.View;
import android.widget.EditText;
import android.widget.ImageButton;
import android.widget.TextView;
import android.widget.Toast;

import com.microsoft.speech.srclientsdk.speech.IRecognitionResultHandler;
import com.microsoft.speech.srclientsdk.speech.Preferences;
import com.microsoft.speech.srclientsdk.speech.RecognitionBaseResult;
import com.microsoft.speech.srclientsdk.speech.RecognitionPartialResult;
import com.microsoft.speech.srclientsdk.speech.RecognitionResult;
import com.microsoft.speech.srclientsdk.speech.SpeechClient;
import com.microsoft.speech.ttsclientsdk.ITTSCallback;
import com.microsoft.speech.ttsclientsdk.Synthesizer;

import java.net.URI;
import java.util.Date;

public class BDHomepage extends AppCompatActivity {
    private ImageButton talkButton;
    private ImageButton transferButton;
    private ImageButton accountinfoButton;
    private ImageButton paymentButton;
    private ImageButton billButton;
    private EditText talkLable;

    private TextView waitText;
    private TextView aveWaitText;
    private TextView testCountText;
    Date curDate = null;
    Date endDate = null;
    boolean getTime = false;

    private Synthesizer m_syn;
    private AudioTrack audioTrack;
    final int SAMPLE_RATE = 16000;
    private long handle = 0;
    private int result= 0 ;

    private AudioRecord mRecorder;
    private  int recorderBufferSize;
    private boolean isRecoding;

    private SpeechClient speechClient;

    final Handler handler = new Handler() {
        @Override
        public void handleMessage(Message msg) {
            super.handleMessage(msg);
            if(msg.arg1 == 0)
                talkLable.setText((String) msg.obj);
            else if(msg.arg1 == 1)
                AccountInfoDialog();
            else if(msg.arg1 == 2)
                TransferDialog ((String) msg.obj);
            else if(msg.arg1 == 3){
                waitText.setText((String) msg.obj);
                int i = Integer.parseInt(testCountText.getText().toString());
                float time = Float.parseFloat(aveWaitText.getText().toString());
                float totalTime = Float.parseFloat((String) msg.obj) + time * i;
                i += 1;
                float aveTime = totalTime/i;
                testCountText.setText(String.valueOf(i));
                aveWaitText.setText(String.valueOf(aveTime));
            }
        }
    };

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
                if(getTime == true) {
                    endDate = new Date(System.currentTimeMillis());
                    long diff = endDate.getTime() - curDate.getTime();
                    long s = (diff / 1000);
                    long ms = (diff - s * 1000);
                    String time = String.valueOf(s) + "." + String.valueOf(ms);
                    Message message = new Message();
                    message.arg1 = 3;
                    message.obj = time;
                    handler.sendMessage(message);
                    getTime = false;
                }
                //write to audio card
                audioTrack.write(data, 0, size);
                return 0;
            }
        });

        //init speech handle
        handle = m_syn.MSTTS_CreateSpeechSynthesizerHandler(handle, getResources().getString(R.string.api_key));

        if(handle == -1){
            Toast.makeText(getApplicationContext(), "Please enter you api key.", Toast.LENGTH_SHORT).show();
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

    private void initSR(){
        try {
            String speechLanguage = getResources().getString(R.string.speech_language);
            URI uri = URI.create("wss://websockets.platform.bing.com/ws/speech/recognize");
            CognitiveServicesAuthorizationProvider authorizationProvider = null;
            authorizationProvider = new CognitiveServicesAuthorizationProvider(getResources().getString(R.string.api_key));
            Preferences preferences = new Preferences(speechLanguage, uri, authorizationProvider, true);
            speechClient = new SpeechClient(preferences);
            speechClient.subscribeToRecognitionPartialResult(new IRecognitionResultHandler() {
                @Override
                public void handleRecognitionResult(RecognitionBaseResult rbr) {
                    Message message = new Message();
                    message.arg1 = 0;
                    message.obj = new String(((RecognitionPartialResult)rbr).displayText);
                    handler.sendMessage(message);
                }
            });

            speechClient.subscribeToRecognitionResult(new IRecognitionResultHandler() {
                @Override
                public void handleRecognitionResult(RecognitionBaseResult rbr) {
                    Message message = new Message();
                    message.arg1 = 0;
                    message.obj = new String(((RecognitionResult)rbr).phrases.get(0).displayText);
                    handler.sendMessage(message);
                    isRecoding = false;
                    curDate = new Date(System.currentTimeMillis());
                    getTime = true;
                }
            });
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

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

        waitText = (TextView) findViewById(R.id.waitText);
        aveWaitText = (TextView) findViewById(R.id.aveWaitText);
        testCountText = (TextView) findViewById(R.id.testCountText);

        isRecoding = false;
        initAudioTrack();
        initSynthesizer();
        AsyncTask.execute(new Runnable() {
            @Override
            public void run() {
                initSR();
            }
        });


        talkButton.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                if(isRecoding){
                    isRecoding = false;
                }else{
                    isRecoding = true;
                    talkLable.setText("");
                    new RecorderAsyncTask().executeOnExecutor(AsyncTask.THREAD_POOL_EXECUTOR,"");
                }
            }
        });

        transferButton.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                Intent intent=new Intent(BDHomepage.this,BDTransfer.class);
                intent.putExtra("data", "");
                startActivity(intent);
            }
        });

        accountinfoButton.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                Intent intent=new Intent(BDHomepage.this,BDAccountinfo.class);
                intent.putExtra("data", "");
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


    private class RecorderAsyncTask extends AsyncTask<String, Integer, String> {
        @Override
        protected String doInBackground(String... params) {
            byte[] audiodata = new byte[recorderBufferSize];
            int readcount = 0;
            int recognizeStatus = 0;
            //initSR();
            speechClient.SpeechClientInit();
            Message message = new Message();
            message.arg1 = 0;
            message.obj = "初始化完毕";
            handler.sendMessage(message);
            mRecorder.startRecording();

            while (isRecoding == true) {
                readcount = mRecorder.read(audiodata, 0, recorderBufferSize);
                if (AudioRecord.ERROR_INVALID_OPERATION != readcount) {
                    speechClient.recognize(audiodata, recognizeStatus);
                    if(recognizeStatus == 0){
                        recognizeStatus = 1;
                    }
                }
            }
            return "";
        }

        @Override
        protected void onPreExecute(){
            recorderBufferSize = AudioRecord.getMinBufferSize(16000, AudioFormat.CHANNEL_IN_MONO, AudioFormat.ENCODING_PCM_16BIT);
            mRecorder = new AudioRecord(MediaRecorder.AudioSource.MIC, 16000, AudioFormat.CHANNEL_IN_MONO, AudioFormat.ENCODING_PCM_16BIT, recorderBufferSize * 2);
        }

        @Override
        protected void onPostExecute(String result) {
            speechClient.recognize(null, -1);
            speechClient.SpeechClientClose();
            mRecorder.stop();
            mRecorder.release();
            mRecorder = null;
            String data = talkLable.getText().toString();
            if((data.indexOf("多少") != -1) || (data.indexOf("查询") != -1) || (data.indexOf("余额") != -1)){
                Message message = new Message();
                message.arg1 = 1;
                handler.sendMessage(message);
                AsyncTask.execute(new Runnable() {
                    @Override
                    public void run() {
                        m_syn.MSTTS_SetOutput(handle, null, m_syn);
                        m_syn.MSTTS_Speak(handle, "您的账户余额为：1458485.45元", 0);
                    }
                });

                //Intent intent=new Intent(BDHomepage.this,BDAccountinfo.class);
                //intent.putExtra("data", "speak");
                //startActivity(intent);
            }else if((data.indexOf("转账") != -1)){
                Message message = new Message();
                message.arg1 = 2;
                message.obj = data;
                handler.sendMessage(message);

                AsyncTask.execute(new Runnable() {
                    @Override
                    public void run() {
                        m_syn.MSTTS_SetOutput(handle, null, m_syn);
                        m_syn.MSTTS_Speak(handle, "已填好您的转账请求，请确认", 0);
                    }
                });
                //Intent intent=new Intent(BDHomepage.this,BDTransfer.class);
                //intent.putExtra("data", data);
                //startActivity(intent);
            }else{
                m_syn.MSTTS_Speak(handle, "对不起，我不知道你在说什么", 0);
            }
        }
    }

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

        String[] name = data.split("给|转账");

        if(name.length > 2){
            mUserName.setText(name[1]);
            mPassword.setText(String.valueOf(NumberFormatUtil.convert(name[2])));
        }

        AlertDialog longinDialog = new AlertDialog.Builder(this)
                .setTitle("转账")
                .setView(trsndgrtDialogView)
                .setPositiveButton("确定", null)
                .setNeutralButton("取消", null)
                .create();
        longinDialog.show();
    }
}
