package com.example.kubank

import android.content.Context
import android.content.Intent
import android.os.Bundle
import android.util.Log
import android.view.View
import android.widget.Button
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import org.json.JSONObject


class Agent : AppCompatActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_agent)


        val btnD: Button = findViewById(R.id.btn_deposit)
        val btnDCancel: Button = findViewById(R.id.btn_deposit_cancel)
        val btnW: Button = findViewById(R.id.btn_withdraw)
        val btnWCancel: Button = findViewById(R.id.btn_withdraw_cancel)

        Thread(Runnable {
            val sharedPref = getSharedPreferences(resources.getString(R.string.app_name), Context.MODE_PRIVATE)
            val token: String = sharedPref.getString("token", "").toString()
            val resp: Response? = Helper.requestGet("http://165.22.66.105/android/agentstatus?token=$token")
            if (resp == null) {
                runOnUiThread {
                    Toast.makeText(applicationContext, "No response was received from the server", Toast.LENGTH_LONG).show()
                }
            } else{
                val err: Int? = resp.getInt("err")
                if (err == -1) {
                    val msg: String? = resp.getString("message")
                    runOnUiThread {
                        Toast.makeText(applicationContext, msg, Toast.LENGTH_LONG).show()
                    }
                    val int = Intent(this, User::class.java)
                    startActivity(int)
                } else {
                    val cash: Boolean? = resp.getBoolean("deliverCash")
                    val height = dpToPx(80, applicationContext)
                    if(cash != null){

                        if(cash){
                            runOnUiThread {
                                btnD.visibility = View.GONE
                            }
                        } else {
                            runOnUiThread {
                                btnDCancel.visibility = View.GONE
                            }
                        }
                    }
                    val emoney: Boolean? = resp.getBoolean("deliverEMoney")
                    Log.e("cash", cash.toString())
                    Log.e("emoney", emoney.toString())
                    if(emoney != null){
                        if(emoney) {
                            runOnUiThread {
                                btnW.visibility = View.GONE
                            }
                        } else {
                            runOnUiThread {
                                btnWCancel.visibility = View.GONE
                            }
                        }
                    }
                }
            }
        }).start()



        // User wants to deliver money (user gives cash, agent delivers E-money)
        btnD.setOnClickListener {
            becomeAgentCash()
        }

        // User wants to deliver money (user gives E-money, agent delivers cash)
        btnW.setOnClickListener {
            becomeAgentEMoney()
        }

        // User no longer wants to deliver money
        btnDCancel.setOnClickListener {
            stopAgent("cash")
        }
        // User no longer wants to deliver e-money
        btnWCancel.setOnClickListener {
            stopAgent("emoney")
        }

    }

    fun dpToPx(dp: Int, context: Context): Int {
        val density = context.resources.displayMetrics.density
        return Math.round(dp.toFloat() * density)
    }

    private fun stopAgent(type: String){
        Thread(Runnable {
            val json = JSONObject()
            val sharedPref = getSharedPreferences(resources.getString(R.string.app_name), Context.MODE_PRIVATE)
            val token: String = sharedPref.getString("token", "").toString()
            json.put("token", token)
            json.put("delivertype", type)
            val resp: Response? = Helper.requestPost("http://165.22.66.105/android/stopAgent", json)
            if (resp == null) {
                runOnUiThread {
                    Toast.makeText(applicationContext, "No response was received from the server", Toast.LENGTH_LONG).show()
                }
            } else {
                val err: Int? = resp.getInt("err")
                if (err == -1) {
                    val msg: String? = resp.getString("message")
                    runOnUiThread {
                        Toast.makeText(applicationContext, msg, Toast.LENGTH_LONG).show()
                    }
                } else {
                        val ty = if(type == "cash"){
                            "cash"
                        } else{
                            "e-money"
                        }
                    runOnUiThread {
                        Toast.makeText(
                            applicationContext,
                            "You are no longer an agent delivering $ty",
                            Toast.LENGTH_LONG
                        ).show()
                    }
                    val int = Intent(this, User::class.java)
                    startActivity(int)
                    finish()
                }
            }
        }).start()
    }

    private fun becomeAgentCash(){
        val int = Intent(this, AgentCash::class.java)
        startActivity(int)
    }

    private fun becomeAgentEMoney(){
        val int = Intent(this, AgentEMoney::class.java)
        startActivity(int)
    }
}