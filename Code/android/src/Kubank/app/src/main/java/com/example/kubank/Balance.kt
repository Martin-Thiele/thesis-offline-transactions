package com.example.kubank

import android.content.Context
import android.content.Intent
import android.os.Bundle
import android.util.Log
import android.view.View
import android.widget.TextView
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity


class Balance : AppCompatActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(com.example.kubank.R.layout.activity_balance)
        getBalance()
    }

    private fun getBalance() {
        // request from server
        // set text
        Thread(Runnable {
            val sharedPref = getSharedPreferences(resources.getString(com.example.kubank.R.string.app_name), Context.MODE_PRIVATE)
            val token: String = sharedPref.getString("token", "").toString()
            val resp: Response? = Helper.requestGet("http://165.22.66.105/android/balance?token=$token")
            if (resp == null) {
                runOnUiThread {
                    Toast.makeText(applicationContext, "No response was received from the server", Toast.LENGTH_LONG).show()
                    val int = Intent(this, User::class.java)
                    startActivity(int)
                }
            } else {
                val err: Int? = resp.getInt("err")
                if (err == -1) {
                    val msg: String? = resp.getString("message")
                    runOnUiThread {
                        Toast.makeText(applicationContext, msg, Toast.LENGTH_LONG).show()
                        val int = Intent(this, User::class.java)
                        startActivity(int)
                    }
                } else {
                    runOnUiThread {
                        val bal = resp.getString("balance").toBigDecimal()
                        val baltext: TextView = findViewById<View>(R.id.tv_balanceval) as TextView
                        baltext.text = bal.toString()
                    }
                }
            }
        }).start()
    }
}