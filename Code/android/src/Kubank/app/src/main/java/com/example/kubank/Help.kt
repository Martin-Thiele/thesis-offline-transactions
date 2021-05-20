package com.example.kubank

import android.content.Context
import android.content.Intent
import android.os.Bundle
import android.util.Log
import android.widget.Button
import android.widget.EditText
import android.widget.TextView
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import org.json.JSONObject


class Help : AppCompatActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_help)

        val btn: Button = findViewById(R.id.btn_help)

        // check for clicks
        btn.setOnClickListener {
            var stoprunning = false
            //POST request
            Thread(Runnable {
                val json = JSONObject()
                val sharedPref = getSharedPreferences(resources.getString(R.string.app_name), Context.MODE_PRIVATE)
                val token: String = sharedPref.getString("token", "").toString()
                val etHelp = findViewById<EditText>(R.id.et_help).text.toString()
                json.put("question", etHelp)
                json.put("token", token)
                val resp: Response? = Helper.requestPost("http://165.22.66.105/android/help", json)
                if(resp == null){
                    runOnUiThread {
                        Toast.makeText(applicationContext, "No response was received from the server", Toast.LENGTH_LONG).show()
                    }
                    stoprunning = true
                }

                if(!stoprunning) {
                    val err: Int? = resp?.getInt("err")
                    if (err == -1) {
                        val msg: String? = resp.getString("message")
                        runOnUiThread {
                            Toast.makeText(applicationContext, msg, Toast.LENGTH_LONG).show()
                        }
                        stoprunning = true
                    }
                }

                if(!stoprunning){
                    // Success
                    this.runOnUiThread {
                        Toast.makeText(applicationContext, "Your question was sent!", Toast.LENGTH_SHORT).show()
                        val int = Intent(this, User::class.java)
                        startActivity(int)
                    }
                }

            }).start()
        }
    }
}