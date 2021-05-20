package com.example.kubank

import android.preference.PreferenceManager
import android.util.Log
import org.json.JSONObject
import java.io.*
import java.net.HttpURLConnection
import java.net.URL


class Response(json: String) : JSONObject(json) {
    val type: String? = this.optString("type")
    val data = this.optJSONArray("data")
        ?.let { 0.until(it.length()).map { i -> it.optJSONObject(i) } } // returns an array of JSONObject
        ?.map { Set(it.toString()) } // transforms each JSONObject of the array into Foo
}

class Set(json: String) : JSONObject(json) {
    val id = this.optInt("id")
    val title: String? = this.optString("title")
}

object Helper {

    const val timeout = 1000;

    private fun readStream(`is`: InputStream): String {
        val sb = StringBuilder()
        val r = BufferedReader(InputStreamReader(`is`), 1000)
        var l: String? = r.readLine()
        while (l != null) {
            sb.append(l)
            l = r.readLine()
        }
        `is`.close()
        return sb.toString()
    }

    fun requestGet(urlin: String): Response?{
        var output : Response? = null
        val url = URL(urlin)
        val urlConnection = url.openConnection() as HttpURLConnection
        urlConnection.connectTimeout = timeout
        urlConnection.readTimeout = timeout
        try {
            val reader: InputStream = BufferedInputStream(urlConnection.inputStream)
            val str = readStream(reader)
            output = Response(str)
        } catch(e: Exception) {
            Log.e("GET", "Exception ", e)
        } finally {
            urlConnection.disconnect()
        }
        return output
    }

    fun requestPost(urlin: String, json: JSONObject): Response?{
        var output : Response? = null
        val url = URL(urlin)
        val urlConnection = url.openConnection() as HttpURLConnection
        urlConnection.requestMethod = "POST"
        urlConnection.connectTimeout = timeout
        urlConnection.readTimeout = timeout
        urlConnection.setRequestProperty("Content-Type", "application/json; utf-8")
        urlConnection.setRequestProperty("Accept", "application/json")
        urlConnection.doOutput = true
        try {
            val os: OutputStream = urlConnection.outputStream
            val input = json.toString().toByteArray(Charsets.UTF_8)
            os.write(input, 0, input.size)
            val reader: InputStream = BufferedInputStream(urlConnection.inputStream)
            val str = readStream(reader)
            output = Response(str)
        } catch(e: Exception) {
            Log.e("POST", "Exception ", e)
        } finally {
            urlConnection.disconnect()
        }
        return output
    }
}