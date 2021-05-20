package com.example.kubank

import android.content.Context
import android.content.Intent
import androidx.appcompat.app.AppCompatActivity
import android.os.Bundle
import android.util.Log
import android.widget.EditText
import android.widget.Button
import android.widget.Toast
import org.json.JSONObject

class Send : AppCompatActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_send)

        val btn: Button = findViewById(R.id.btn_next)
        btn.setOnClickListener {
            //POST request to verify input is correct before entering PIN
            Thread(Runnable {
                val json = JSONObject()
                val etAmount = findViewById<EditText>(R.id.et_amount).text.toString()
                val etRecipient = findViewById<EditText>(R.id.et_recipient).text.toString()
                val etReason = findViewById<EditText>(R.id.et_reason).text.toString()
                val sharedPref = getSharedPreferences(resources.getString(com.example.kubank.R.string.app_name), Context.MODE_PRIVATE)
                val token: String = sharedPref.getString("token", "").toString()
                json.put("amount", etAmount)
                json.put("recipient", etRecipient)
                json.put("reason", etReason)
                json.put("token", token)
                val resp: Response? = Helper.requestPost("http://165.22.66.105/android/send", json)
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
                        -2 -> {
                            // input was valid
                            this.runOnUiThread {
                                val int = Intent(this, Send2::class.java)
                                val desc = "You are about to send $etAmount E-GNF to $etRecipient"
                                int.putExtra("description", desc)
                                int.putExtra("amount", etAmount)
                                int.putExtra("reason", etReason)
                                int.putExtra("recipient", etRecipient)

                                startActivity(int)
                            }
                        }
                        -50 -> {
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
                                Toast.makeText(applicationContext, msg, Toast.LENGTH_LONG).show()
                            }
                        }
                    }
                }
            }).start()
        }
    }


}