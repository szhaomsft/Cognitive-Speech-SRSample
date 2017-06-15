package com.microsoft.speech.bankdemo;

import android.support.v7.app.AppCompatActivity;
import android.os.Bundle;
import android.view.View;
import android.widget.Button;
import android.widget.EditText;
import android.widget.Toast;

public class BDTransfer extends AppCompatActivity {
    private EditText nameText;
    private EditText amountText;
    private Button confirmButton;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_bdtransfer);

        nameText = (EditText)findViewById(R.id.nameText);
        amountText = (EditText)findViewById(R.id.amountText);
        confirmButton = (Button)findViewById(R.id.confirmButton);

        confirmButton.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                if(nameText.getText().toString().isEmpty() || amountText.getText().toString().isEmpty()){
                    Toast.makeText(getApplicationContext(), "请正确填写转账信息", Toast.LENGTH_SHORT).show();
                }else{
                    Toast.makeText(getApplicationContext(), "转账成功", Toast.LENGTH_SHORT).show();
                    finish();
                }
            }
        });
    }
}
