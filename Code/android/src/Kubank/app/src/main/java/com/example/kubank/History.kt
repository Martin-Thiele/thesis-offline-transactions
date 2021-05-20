package com.example.kubank

import android.annotation.SuppressLint
import android.app.Activity
import android.app.AlertDialog
import android.content.Context
import android.content.Intent
import android.graphics.Color
import android.graphics.Typeface
import android.os.Bundle
import android.text.InputType
import android.util.Log
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.*
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat.startActivity
import org.json.JSONArray
import org.json.JSONObject
import java.text.SimpleDateFormat


class History : AppCompatActivity() {
    private var expandableListView: ExpandableListView? = null
    private var adapter: ExpandableListAdapter? = null
    private var titleList: JSONArray = JSONArray()
    @SuppressLint("SetTextI18n")
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_history)
        expandableListView = findViewById(R.id.expendableList)
        if (expandableListView != null) {
            Thread(Runnable {
                val sharedPref = getSharedPreferences(resources.getString(R.string.app_name), Context.MODE_PRIVATE)
                val token: String = sharedPref.getString("token", "").toString()
                val resp: Response? = Helper.requestGet("http://165.22.66.105/android/history?token=$token")
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
                        val transactions: JSONArray = resp.getJSONArray("transactions")
                        if(transactions.length() == 0){
                            this.runOnUiThread {
                                val baltext: TextView = findViewById<View>(R.id.tv_no_transactions) as TextView
                                baltext.text = "No transactions found"
                            }
                        } else {
                            //Retrieve the values
                            titleList = transactions
                            adapter = CustomExpandableListAdapter(this, titleList, transactions)
                            runOnUiThread {
                                expandableListView!!.setAdapter(adapter)
                                expandableListView!!.setOnGroupExpandListener {
                                }
                                expandableListView!!.setOnGroupCollapseListener {
                                }
                                expandableListView!!.setOnChildClickListener { _, _, _, _, _ ->
                                    false
                                }
                            }
                        }
                    }
                }
            }).start()

        }
    }
}

class CustomExpandableListAdapter internal constructor(
        private val context: Context,
        private val titleList: JSONArray,
        private val dataList: JSONArray
) : BaseExpandableListAdapter() {

    override fun getChild(listPosition: Int, expandedListPosition: Int): Any {
        return this.dataList[listPosition]
    }
    override fun getChildId(listPosition: Int, expandedListPosition: Int): Long {
        return expandedListPosition.toLong()
    }

    @SuppressLint("SetTextI18n")
    override fun getChildView(
            listPosition: Int,
            expandedListPosition: Int,
            isLastChild: Boolean,
            convertView: View?,
            parent: ViewGroup
    ): View {
        var cv = convertView
        val expandedListText = getChild(listPosition, expandedListPosition)
        val js = expandedListText as JSONObject
        val act = this.context as Activity
        if (cv == null) {
            val layoutInflater = this.context.getSystemService(Context.LAYOUT_INFLATER_SERVICE) as LayoutInflater
            if(js["Status"] == "Pending" && js["From"] == "you") {
                cv = layoutInflater.inflate(R.layout.history_buttons, null)
                val confirmButton: View = cv.findViewById<View>(R.id.btn_confirm)
                val am = js["Amount"].toString().toBigDecimal()
                val fee = js["Fee"].toString().toBigDecimal()
                val amAfterFee = am - (am*fee) // should ideally be done in big decimals
                val popupmsgconfirm: String
                val popupmsgdecline: String
                val successmsg: String
                when {
                    js["Type"] == "Deposit" -> { // agent confirms users handover
                        popupmsgconfirm = "Enter your PIN to confirm the transfer of $amAfterFee E-GNF in exchange for $am GNF"
                        successmsg = "Success! Please make sure you receive your $am GNF"
                        popupmsgdecline = "Are you sure you wish to decline the transfer of $amAfterFee E-GNF in exchange for $am GNF?"
                    }
                    js["Type"] == "Withdrawal" -> { // user confirms agents handover
                        popupmsgconfirm = "Enter your PIN to confirm the transfer of $am E-GNF in exchange for $amAfterFee GNF"
                        successmsg = "Success! Please make sure you receive your $amAfterFee GNF"
                        popupmsgdecline = "Are you sure you wish to decline the transfer of $am E-GNF in exchange for $amAfterFee GNF?"
                    }
                    else -> { // request
                        popupmsgconfirm = "Enter your PIN to confirm the transfer of $am E-GNF"
                        successmsg = "Success!"
                        popupmsgdecline = "Are you sure to decline the request of $am E-GNF?"
                    }
                }
                confirmButton.setOnClickListener {
                    // Set an EditText view to get user input
                    val alert = AlertDialog.Builder(this.context)
                    alert.setTitle("Confirm transfer")
                    alert.setMessage(popupmsgconfirm)
                    val input = EditText(this.context)
                    input.inputType = InputType.TYPE_NUMBER_VARIATION_PASSWORD
                    alert.setView(input)
                    alert.setIcon(android.R.drawable.ic_dialog_alert)
                    alert.setPositiveButton("Confirm") { _, _ ->
                        Thread(Runnable {
                            val json = JSONObject()
                            val sharedPref = this.context.getSharedPreferences(this.context.resources.getString(R.string.app_name), Context.MODE_PRIVATE)
                            val token: String = sharedPref.getString("token", "").toString()
                            json.put("token", token)
                            json.put("id", js["ID"])
                            val pin = input.text.toString()
                            json.put("pin", pin)
                            val resp: Response? = Helper.requestPost(
                                    "http://165.22.66.105/android/confirm",
                                    json
                            )
                            if (resp == null) {
                                act.runOnUiThread {
                                    Toast.makeText(
                                            this.context.applicationContext,
                                            "No response was received from the server",
                                            Toast.LENGTH_LONG
                                    ).show()
                                }
                            } else {
                                val err2: Int? = resp.getInt("err")
                                val msg: String? = resp.getString("message")
                                if (msg != null) {
                                    when (err2) {
                                        -1 -> {
                                            act.runOnUiThread {
                                                Toast.makeText(
                                                        this.context.applicationContext,
                                                        msg,
                                                        Toast.LENGTH_LONG
                                                ).show()
                                            }
                                        }
                                        -50 -> { // Session expired
                                            act.runOnUiThread {
                                                Toast.makeText(
                                                        this.context.applicationContext,
                                                        msg,
                                                        Toast.LENGTH_LONG
                                                ).show()
                                            }
                                            val int = Intent(this.context, Login::class.java)
                                            this.context.startActivity(int)
                                            this.context.finish()
                                        }
                                        else -> {
                                            act.runOnUiThread {
                                                Toast.makeText(
                                                        this.context.applicationContext,
                                                        successmsg,
                                                        Toast.LENGTH_LONG
                                                ).show()
                                                val int = Intent(this.context, History::class.java)
                                                this.context.startActivity(int)
                                                this.context.finish()
                                            }
                                        }
                                    }
                                } else {
                                    act.runOnUiThread {
                                        Toast.makeText(
                                                this.context.applicationContext,
                                                "An unknown error occured. Please try again later",
                                                Toast.LENGTH_LONG
                                        ).show()
                                    }
                                }
                            }
                        }).start()
                    }
                    alert.setNegativeButton("Cancel", null)
                    alert.show()
                }

                val declineButton: View = cv.findViewById<View>(R.id.btn_decline)
                    declineButton.setOnClickListener {

                    // Set an EditText view to get user input
                    val alert = AlertDialog.Builder(this.context)
                    alert.setTitle("Decline transfer")
                    alert.setMessage(popupmsgdecline)
                    alert.setIcon(android.R.drawable.ic_dialog_alert)
                    alert.setPositiveButton("Decline transfer") { _, _ ->
                        Thread(Runnable {
                            val json = JSONObject()
                            val sharedPref = this.context.getSharedPreferences(this.context.resources.getString(R.string.app_name), Context.MODE_PRIVATE)
                            val token: String = sharedPref.getString("token", "").toString()
                            json.put("token", token)
                            json.put("id", js["ID"])
                            val resp: Response? = Helper.requestPost(
                                    "http://165.22.66.105/android/decline",
                                    json
                            )
                            if (resp == null) {
                                act.runOnUiThread {
                                    Toast.makeText(
                                            this.context.applicationContext,
                                            "No response was received from the server",
                                            Toast.LENGTH_LONG
                                    ).show()
                                }
                            } else {
                                val err2: Int? = resp.getInt("err")
                                val msg: String? = resp.getString("message")
                                if (msg != null) {
                                    when (err2) {
                                        -1 -> {
                                            act.runOnUiThread {
                                                Toast.makeText(
                                                        this.context.applicationContext,
                                                        msg,
                                                        Toast.LENGTH_LONG
                                                ).show()
                                            }
                                        }
                                        -50 -> { // Session expired
                                            act.runOnUiThread {
                                                Toast.makeText(
                                                        this.context.applicationContext,
                                                        msg,
                                                        Toast.LENGTH_LONG
                                                ).show()
                                            }
                                            val int = Intent(this.context, Login::class.java)
                                            this.context.startActivity(int)
                                            this.context.finish()
                                        }
                                        else -> {
                                            act.runOnUiThread {
                                                Toast.makeText(
                                                        this.context.applicationContext,
                                                        "You have declined this transfer",
                                                        Toast.LENGTH_LONG
                                                ).show()
                                                val int = Intent(this.context, History::class.java)
                                                this.context.startActivity(int)
                                                this.context.finish()
                                            }
                                        }
                                    }
                                } else {
                                    act.runOnUiThread {
                                        Toast.makeText(
                                                this.context.applicationContext,
                                                "An unknown error occurred. Please try again later",
                                                Toast.LENGTH_LONG
                                        ).show()
                                    }
                                }
                            }
                        }).start()
                    }
                    alert.setNegativeButton("Cancel", null)
                    alert.show()
                }
            } else{
                cv = layoutInflater.inflate(R.layout.history_child, null)
            }
        }

        // Child view
        val childview = cv!!.findViewById<RelativeLayout>(R.id.child)
        if(childview != null){
            childview.setBackgroundColor(Color.parseColor("#E8E8E9"))
            val params: LinearLayout.LayoutParams = childview.getLayoutParams() as LinearLayout.LayoutParams
            params.height = ViewGroup.LayoutParams.WRAP_CONTENT
            params.width = ViewGroup.LayoutParams.WRAP_CONTENT
            childview.layoutParams = params
        }

        // Display date in child view
        val date = cv.findViewById<TextView>(R.id.tv_date)
        if (date != null) {
            if(js["Complete_time"] != "0001-01-01T00:00:00") { // Minimum time
                val pformat = SimpleDateFormat("dd/MM-yyyy")
                var d = SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss").parse(js["Complete_time"] as String)
                if(d != null) {
                    date.text = pformat.format(d)
                } else{
                    date.text = ""
                }
            } else{
                date.text = ""
            }
        }
        // Display reason in child view
        val reas = cv.findViewById<TextView>(R.id.tv_reason)
        if(reas != null){

            val iscomplete = js["Complete_time"] != "0001-01-01T00:00:00"
            val am = js["Amount"].toString().toBigDecimal()
            val fee = js["Fee"].toString().toBigDecimal()
            val amAfterFee = am - (am*fee) // should ideally be done in big decimals

            // use the reason field to show how much the user paid for the deposit/withdrawal
            if(js["Type"] == "Deposit") {
                if(iscomplete){
                    if(js["From"] == "you") { // user is the agent
                        reas.text = "You received $am GNF"
                    } else{
                        reas.text = "You deposited $am GNF"
                    }
                } else{
                    if(js["From"] == "you") { // user is the agent
                        reas.text = "You're about to transfer $amAfterFee E-GNF. Please contact the recipient to determine a meeting spot. Please receive $am GNF before confirming."
                    } else{
                        reas.text = "You're about to hand over $am GNF to the agent. You can contact the agent to determine a meeting spot. Please hand over the cash first, the agent will confirm after."
                    }
                }
            } else if(js["Type"] == "Withdrawal") {
                if (iscomplete) {
                    if (js["From"] == "you") { // user is the sender
                        reas.text = "You withdrew $amAfterFee GNF"
                    } else {
                        reas.text = "You received $am E-GNF"
                    }
                } else {
                    if (js["From"] == "you") { // user is the sender
                        reas.text = "You're about transfer $am E-GNF. You can contact the agent to determine a meeting spot. Please receive $amAfterFee GNF before confirming."
                    } else {
                        reas.text = "You're about to hand over $am GNF. Please contact the recipient to determine a meeting spot. Please hand over the cash first, the recipient will confirm after."
                    }
                }

                // show the reason for the transaction
            } else {
                if(js["Reason"] == "" || js["Reason"] == "null"){
                    reas.text = ""
                } else {
                    reas.text = js["Reason"].toString()
                }
            }
        }
        // Display transaction type
        val type = cv.findViewById<TextView>(R.id.tv_type)
        if(type != null){
            type.text = js["Type"] as String
        }

        return cv
    }
    override fun getChildrenCount(listPosition: Int): Int {
        return 1
    }
    override fun getGroup(listPosition: Int): Any {
        return this.titleList[listPosition]
    }
    override fun getGroupCount(): Int {
        return this.titleList.length()
    }
    override fun getGroupId(listPosition: Int): Long {
        return listPosition.toLong()
    }
    override fun getGroupView(
            listPosition: Int,
            isExpanded: Boolean,
            convertView: View?,
            parent: ViewGroup
    ): View {
        var cv = convertView
        val js: JSONObject = getGroup(listPosition) as JSONObject
        if (cv == null) {
            val layoutInflater = this.context.getSystemService(Context.LAYOUT_INFLATER_SERVICE) as LayoutInflater
            cv = layoutInflater.inflate(R.layout.history_parent, null)
        }


        // Parent view
        // set sender and recipient
        val fromto: TextView = cv!!.findViewById<TextView>(R.id.tv_fromto)
        //fromto.setTypeface(null, Typeface.BOLD)
        val str = "${js["From"]} -> ${js["To"]}"
        fromto.text = str

        // set amount
        val bal = cv.findViewById<TextView>(R.id.tv_balanceval)
        if(bal != null){
            val am = js["Amount"].toString().toBigDecimal()
            val fee = js["Fee"].toString().toBigDecimal()
            val amAfterFee = am - (am*fee) // should ideally be done in big decimals
            val str2 = if(js["Type"] == "Deposit"){
                "$amAfterFee"
            } else{
                "$am"
            }
            bal.text = str2
        }

        // set status
        val sta = cv.findViewById<TextView>(R.id.tv_status)
        if(sta != null){
            if(js["Status"] == "Complete"){
                sta.setTextColor(Color.parseColor("#4C9732"))
            } else{
                sta.setTextColor(Color.parseColor("#FF000000"))
            }
            sta.text = "[${js["Status"]}]"
        }




        return cv
    }
    override fun hasStableIds(): Boolean {
        return false
    }
    override fun isChildSelectable(listPosition: Int, expandedListPosition: Int): Boolean {
        return true
    }
}