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

class AgentEMoney : AppCompatActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_agentemoney)

        val btn: Button = findViewById(R.id.btn)
        btn.setOnClickListener {
            //POST request to verify input is correct before entering PIN
            Thread(Runnable {
                val json = JSONObject()
                val etAmount = findViewById<EditText>(R.id.et_amount).text.toString()
                val etFee = findViewById<EditText>(R.id.et_fee).text.toString()
                val sharedPref = getSharedPreferences(resources.getString(R.string.app_name), Context.MODE_PRIVATE)
                val token: String = sharedPref.getString("token", "").toString()
                json.put("amount", etAmount)
                json.put("fee", etFee)
                json.put("token", token)
                json.put("delivertype", "emoney")
                val resp: Response? = Helper.requestPost("http://165.22.66.105/android/becomeAgent", json)
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
                            Toast.makeText(applicationContext, msg, Toast.LENGTH_SHORT)
                                    .show()
                            val int = Intent(this, Login::class.java)
                            startActivity(int)
                            finish()
                        }
                        else -> {
                            this.runOnUiThread {
                                Toast.makeText(applicationContext, "You are now an agent delivering e-money", Toast.LENGTH_SHORT).show()
                            }
                            val int = Intent(this, User::class.java)
                            startActivity(int)
                            finish()
                        }
                    }
                }
            }).start()
        }
    }


}