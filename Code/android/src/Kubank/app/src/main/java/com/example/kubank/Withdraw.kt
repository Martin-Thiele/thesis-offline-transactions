package com.example.kubank

import android.content.Intent
import androidx.appcompat.app.AppCompatActivity
import android.os.Bundle
import android.util.Log
import android.widget.Button
import android.widget.EditText
import android.widget.Toast
import java.math.BigDecimal

class Withdraw : AppCompatActivity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_withdraw)

        val btn: Button = findViewById(R.id.btn_next)
        btn.setOnClickListener {
            val amount = findViewById<EditText>(R.id.et_amount).text
            if(amount == null){
                runOnUiThread {
                    Toast.makeText(applicationContext, "Amount can't be less than 0.1", Toast.LENGTH_LONG).show()
                }
            } else{

                val amLong = amount.toString().toBigDecimal()
                val minLong = BigDecimal(0.1)
                if(amLong < minLong){
                    runOnUiThread {
                        Toast.makeText(applicationContext, "Amount can't be less than 0.1", Toast.LENGTH_LONG).show()
                    }
                } else {
                    Log.e("amount", amount.toString())
                    val int = Intent(this, Withdraw2::class.java)
                    int.putExtra("amount", amount.toString())
                    startActivity(int)
                    finish()
                }
            }
        }
    }

}