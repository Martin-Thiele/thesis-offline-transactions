package com.example.kubank

import android.content.Intent
import android.os.Bundle
import android.widget.Button
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity


class User : AppCompatActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_user)

        // send
        val btn_send = findViewById<Button>(R.id.btn_send)
        btn_send.setOnClickListener {
            val intent = Intent(this, Send::class.java)
            startActivity(intent)
        }

        // deposit
        val btn_deposit = findViewById<Button>(R.id.btn_deposit)
        btn_deposit.setOnClickListener {
            val intent = Intent(this, Deposit::class.java)
            startActivity(intent)
        }

        // withdraw
        val btn_withdraw = findViewById<Button>(R.id.btn_withdraw)
        btn_withdraw.setOnClickListener {
            val intent = Intent(this, Withdraw::class.java)
            startActivity(intent)
        }

        // request
        val btn_request = findViewById<Button>(R.id.btn_request)
        btn_request.setOnClickListener {
            val intent = Intent(this, Request::class.java)
            startActivity(intent)
        }

        // history
        val btn_agent = findViewById<Button>(R.id.btn_agent)
        btn_agent.setOnClickListener {
            val intent = Intent(this, Agent::class.java)
            startActivity(intent)
        }

        // history
        val btn_history = findViewById<Button>(R.id.btn_history)
        btn_history.setOnClickListener {
            val intent = Intent(this, History::class.java)
            startActivity(intent)
        }


        // balance
        val btn_balance = findViewById<Button>(R.id.btn_balance)
        btn_balance.setOnClickListener {
            val intent = Intent(this, Balance::class.java)
            startActivity(intent)
        }

        // help
        val btn_help = findViewById<Button>(R.id.btn_help)
        btn_help.setOnClickListener {
            val intent = Intent(this, Help::class.java)
            startActivity(intent)
        }

        // sign out
        val btn_signout = findViewById<Button>(R.id.btn_signout)
        btn_signout.setOnClickListener {
            runOnUiThread {
                Toast.makeText(applicationContext, "Logged out!", Toast.LENGTH_SHORT).show()
            }
            val intent = Intent(this, Login::class.java)
            startActivity(intent)
            finish()

        }
    }
}