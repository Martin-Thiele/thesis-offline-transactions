package com.example.kubank

import android.annotation.SuppressLint
import android.content.Context
import android.content.Intent
import androidx.appcompat.app.AppCompatActivity
import android.os.Bundle
import android.widget.EditText
import android.widget.Button
import android.widget.Toast
import org.json.JSONObject

class Request : AppCompatActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_request)

        val btn: Button = findViewById(R.id.btn_request)
        btn.setOnClickListener {
            //POST request to verify input is correct before entering PIN
            Thread(Runnable {
                val json = JSONObject()
                val etAmount = findViewById<EditText>(R.id.et_amount).text.toString()
                val etSender = findViewById<EditText>(R.id.et_sender).text.toString()
                val etReason = findViewById<EditText>(R.id.et_reason).text.toString()
                val sharedPref = getSharedPreferences(resources.getString(R.string.app_name), Context.MODE_PRIVATE)
                val token: String = sharedPref.getString("token", "").toString()
                json.put("amount", etAmount)
                json.put("sender", etSender)
                json.put("reason", etReason)
                json.put("token", token)
                val resp: Response? = Helper.requestPost("http://165.22.66.105/android/request", json)
                if (resp == null) {
                    runOnUiThread {
                        Toast.makeText(applicationContext, "No response was received from the server", Toast.LENGTH_LONG).show()
                    }
                } else {
                    val err: Int? = resp.getInt("err")
                    val msg: String? = resp.getString("message")

                    when (err) {
                        -1 -> {
                            this.runOnUiThread {
                                Toast.makeText(applicationContext, msg, Toast.LENGTH_LONG).show()
                            }
                        }
                        -50 -> { // Session expired
                            this.runOnUiThread {
                                Toast.makeText(applicationContext, msg, Toast.LENGTH_SHORT)
                                    .show()
                            }
                            val int = Intent(this, Login::class.java)
                            startActivity(int)
                            finish()
                        }
                        else -> {
                            this.runOnUiThread {
                                Toast.makeText(applicationContext, "Success!", Toast.LENGTH_SHORT)
                                    .show()
                                val int = Intent(this, History::class.java)
                                startActivity(int)
                            }
                            finish()
                        }
                    }
                }
            }).start()
        }
    }


}