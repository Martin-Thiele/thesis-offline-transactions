package com.example.kubank

import android.content.Context
import android.content.Intent
import android.os.Bundle
import android.widget.Button
import android.widget.EditText
import android.widget.TextView
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import org.json.JSONObject

class Send2 : AppCompatActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_send2)
        val iin = intent
        val b: Bundle? = iin.extras
        val description: TextView = findViewById(R.id.tv_senddescription)

        if (b != null) {
            val desc = b.getString("description")
            if (desc != null) {
                description.text = desc
            } else{
                description.text = ""
            }
        }


        val btn: Button = findViewById(R.id.btn_confirm)
        btn.setOnClickListener {
            Thread(Runnable {
                val etPin = findViewById<EditText>(R.id.et_pin).text.toString()
                val json = JSONObject()
                // fetch intent
                if (b != null) {
                    val amount: String? = b.getString("amount")
                    val recipient: String? = b.getString("recipient")
                    val reason: String? = b.getString("reason")
                    val sharedPref = getSharedPreferences(resources.getString(R.string.app_name), Context.MODE_PRIVATE)
                    val token: String = sharedPref.getString("token", "").toString()
                    json.put("amount", amount)
                    json.put("recipient", recipient)
                    json.put("reason", reason)
                    json.put("pin", etPin)
                    json.put("token", token)
                    // Send request
                    val resp: Response? = Helper.requestPost("http://165.22.66.105/android/send", json)
                    if (resp == null) {
                        runOnUiThread {
                            Toast.makeText(applicationContext, "No response was received from the server", Toast.LENGTH_LONG).show()
                        }
                    } else {
                        val msg: String? = resp.getString("message")
                        val err: Int? = resp.getInt("err")
                        when (err) {
                            -1 -> {
                                runOnUiThread {
                                    Toast.makeText(applicationContext, msg, Toast.LENGTH_LONG).show()
                                }
                            }
                            0 -> {
                                // Success
                                this.runOnUiThread {
                                    Toast.makeText(applicationContext, "Success!", Toast.LENGTH_SHORT).show()
                                    val int = Intent(this, History::class.java)
                                    startActivity(int)
                                }
                                finish()
                            }
                            else -> {
                                this.runOnUiThread {
                                    Toast.makeText(applicationContext, msg, Toast.LENGTH_SHORT)
                                        .show()
                                }
                            }
                        }
                    }
                }
            }).start()
        }
    }

}