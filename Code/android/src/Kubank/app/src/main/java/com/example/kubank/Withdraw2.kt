package com.example.kubank

import android.app.AlertDialog
import android.content.Context
import android.content.Intent
import android.graphics.Color
import android.os.Bundle
import android.util.Log
import android.view.View
import android.view.ViewGroup
import android.widget.AdapterView.OnItemClickListener
import android.widget.ArrayAdapter
import android.widget.ListView
import android.widget.TextView
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity
import org.json.JSONArray
import org.json.JSONObject
import java.math.BigDecimal


class Withdraw2 : AppCompatActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_withdraw2)

        val iin = intent
        val b: Bundle? = iin.extras
        val amount = b!!.getString("amount")

        // GET list of agents within $amount
        Thread(Runnable {
            val sharedPref = getSharedPreferences(resources.getString(R.string.app_name), Context.MODE_PRIVATE)
            val token: String = sharedPref.getString("token", "").toString()
            val type = "withdrawal"
            val resp: Response? = Helper.requestGet("http://165.22.66.105/android/agents?token=$token&amount=$amount&type=$type")
            if (resp == null) {
                runOnUiThread {
                    Toast.makeText(applicationContext, "No response was received from the server", Toast.LENGTH_LONG).show()
                }
                val int = Intent(this, Withdraw::class.java)
                startActivity(int)
            } else {
                val err: Int? = resp.getInt("err")
                if (err == -1) {
                    val msg: String? = resp.getString("message")
                    runOnUiThread {
                        Toast.makeText(applicationContext, msg, Toast.LENGTH_LONG).show()
                    }
                    val int = Intent(this, Withdraw::class.java)
                    startActivity(int)
                } else {
                    runOnUiThread {
                        val agents: JSONArray = resp.getJSONArray("agents")
                        // iterate over them and add them to a listview
                        if (agents.length() > 0) {
                            Log.e("agents", agents.toString())
                            val values = arrayOfNulls<String>(agents.length())
                            val am = amount.toString().toBigDecimal()
                            for (i in 0 until agents.length()) {
                                val agent = agents.getJSONObject(i)
                                val fee = agent["CashFee"].toString().toBigDecimal() * (BigDecimal(100))
                                val amountAfterFee = am - (am * (agent["CashFee"].toString().toBigDecimal()))
                                values[i] = "Phone: ${agent["PhoneNumber"]} \nFee: $fee%. You will receive $amountAfterFee GNF"
                            }
                            val lv = findViewById<ListView>(R.id.listview)

                            lv.adapter = object : ArrayAdapter<String?>(
                                    this,
                                    android.R.layout.simple_list_item_1,
                                    values
                            ) {
                                override fun getView(
                                        position: Int,
                                        convertView: View?,
                                        parent: ViewGroup
                                ): View {
                                    val row = super.getView(position, convertView, parent)
                                    if (position % 2 == 0) {
                                        row.setBackgroundColor(Color.parseColor("#E7E7E7"))
                                    } else {
                                        row.setBackgroundColor(Color.WHITE)
                                    }
                                    return row
                                }
                            }

                            // when clicked on an agent
                            lv.onItemClickListener =
                                    OnItemClickListener { _, _, position, _ ->
                                        val a: JSONObject = agents.getJSONObject(position)
                                        val json = JSONObject()
                                        json.put("token", token)
                                        json.put("amount", amount)
                                        json.put("fee", a["CashFee"]) // ensure fee is correct
                                        json.put("agent", a["ID"])
                                        val amountAfterFee = am - (am * (a["CashFee"].toString().toBigDecimal()))
                                        // Request confirmation
                                        AlertDialog.Builder(this)
                                                .setTitle("Confirm withdrawal")
                                                .setMessage("Are you sure you wish to transfer $am E-GNF in exchange for $amountAfterFee GNF? Once confirmed, please contact the agent.")
                                                .setIcon(android.R.drawable.ic_dialog_alert)
                                                .setPositiveButton("Confirm") { _, _ ->
                                                    Thread(Runnable {
                                                        val resp2: Response? = Helper.requestPost(
                                                                "http://165.22.66.105/android/withdraw",
                                                                json
                                                        )
                                                        if (resp2 == null) {
                                                            runOnUiThread {
                                                                Toast.makeText(
                                                                        applicationContext,
                                                                        "No response was received from the server",
                                                                        Toast.LENGTH_LONG
                                                                ).show()
                                                            }
                                                            val int = Intent(this, Withdraw::class.java)
                                                            startActivity(int)
                                                        } else {
                                                            val err2: Int? = resp2.getInt("err")
                                                            val msg: String? = resp2.getString("message")
                                                            if (msg != null) {
                                                                when (err2) {
                                                                    -1 -> {
                                                                        this.runOnUiThread {
                                                                            Toast.makeText(applicationContext, msg, Toast.LENGTH_LONG
                                                                            ).show()
                                                                        }
                                                                        val int = Intent(this, Withdraw::class.java
                                                                        )
                                                                        startActivity(int)
                                                                    }
                                                                    -50 -> { // Session expired
                                                                        this.runOnUiThread {
                                                                            Toast.makeText(applicationContext, msg, Toast.LENGTH_LONG
                                                                            ).show()
                                                                        }
                                                                        val int = Intent(this, Login::class.java)
                                                                        startActivity(int)
                                                                        finish()
                                                                    }
                                                                    else -> {
                                                                        this.runOnUiThread {
                                                                            Toast.makeText(applicationContext,"Your withdrawal has been requested. Please contact the agent and receive your GNF in exchange for E-GNF", Toast.LENGTH_LONG
                                                                            ).show()
                                                                            val int = Intent(this, History::class.java)
                                                                            startActivity(int)
                                                                            finish()
                                                                        }
                                                                    }
                                                                }
                                                            } else {
                                                                this.runOnUiThread {
                                                                    Toast.makeText(applicationContext, "An unknown error occurred. Please try again later", Toast.LENGTH_LONG
                                                                    ).show()
                                                                }
                                                            }
                                                        }
                                                    }).start()
                                                }
                                                .setNegativeButton("Cancel", null)
                                                .show()


                                    }
                        } else {
                            // No agents found
                            val noagents: TextView =
                                    findViewById<View>(R.id.tv_no_agents) as TextView
                            noagents.text = "No agents found delivering $amount GNF"
                        }
                    }
                }
            }
        }).start()
    }

}