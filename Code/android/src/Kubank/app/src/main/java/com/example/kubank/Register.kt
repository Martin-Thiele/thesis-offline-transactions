package com.example.kubank

import android.content.Context
import android.content.Intent
import androidx.appcompat.app.AppCompatActivity
import android.os.Bundle
import android.widget.Button
import android.widget.EditText
import android.widget.Toast
import org.json.JSONObject

class Register : AppCompatActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_register)


        val btn: Button = findViewById(R.id.btn_signup)
        btn.setOnClickListener{

            var stoprunning = false

            //POST request
            Thread(Runnable {
                val json = JSONObject()
                val etPhone = findViewById<EditText>(R.id.et_phone).text.toString()
                val etPin = findViewById<EditText>(R.id.et_pin).text.toString()
                json.put("number", etPhone)
                json.put("pin", etPin)
                val resp: Response? = Helper.requestPost("http://165.22.66.105/android/register", json)
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
                    val token: String? = resp?.getString("token")
                    if(token == null){
                        this.runOnUiThread {
                            Toast.makeText(applicationContext, "Could not log you in. Please try again later", Toast.LENGTH_LONG).show()
                        }
                    } else {
                        // Success
                        val preference = getSharedPreferences(resources.getString(R.string.app_name), Context.MODE_PRIVATE)
                        val editor = preference.edit()
                        editor.putString("token", token) // unique token
                        editor.putBoolean("isLoggedIn", true)
                        editor.apply()

                        this.runOnUiThread {
                            Toast.makeText(applicationContext, "Success!", Toast.LENGTH_SHORT).show()
                        }
                        val int = Intent(this, User::class.java)
                        startActivity(int)
                        finish()
                    }
                }

            }).start()
        }
    }
}