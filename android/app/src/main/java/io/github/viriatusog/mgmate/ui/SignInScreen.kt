package io.github.viriatusog.mgmate.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.systemBarsPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Bolt
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import io.github.viriatusog.mgmate.AppViewModel

/** Asked once: where the server is and who is signing in. After that the fingerprint opens the app. */
@Composable
fun SignInScreen(vm: AppViewModel) {
    var server by rememberSaveable { mutableStateOf(vm.session.serverUrl ?: "") }
    var username by rememberSaveable { mutableStateOf("") }
    var password by rememberSaveable { mutableStateOf("") }
    val ready = server.isNotBlank() && username.isNotBlank() && password.isNotEmpty() && !vm.signingIn
    val submit = { if (ready) vm.signIn(server, username, password) }

    Column(
        Modifier
            .fillMaxSize()
            .background(Volt.Bg)
            .systemBarsPadding()
            .imePadding()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 24.dp, vertical = 40.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Logo()
        Text("MGMate", color = Volt.Ink, fontSize = 26.sp, fontWeight = FontWeight.Bold)
        Text("Connect to your own MGMate server", color = Volt.Ink3, fontSize = 14.sp)

        Field("Server address", server, { server = it }, "http://192.168.1.50:4020", KeyboardType.Uri, ImeAction.Next)
        Text(
            "The address you open MGMate at in your browser.",
            color = Volt.Ink3,
            fontSize = 12.sp,
            modifier = Modifier.fillMaxWidth(),
        )
        Field("Username", username, { username = it }, "Your MGMate sign-in", KeyboardType.Email, ImeAction.Next)
        Field("Password", password, { password = it }, "", KeyboardType.Password, ImeAction.Done, secret = true, onDone = submit)

        vm.signInError?.let { Text(it, color = Volt.Alert, fontSize = 14.sp, modifier = Modifier.fillMaxWidth()) }

        Button(
            onClick = submit,
            enabled = ready,
            modifier = Modifier.fillMaxWidth().height(50.dp),
            shape = RoundedCornerShape(14.dp),
            colors = ButtonDefaults.buttonColors(containerColor = Volt.Accent, contentColor = Volt.AccentInk),
        ) {
            Text(if (vm.signingIn) "Connecting" else "Connect", fontSize = 16.sp, fontWeight = FontWeight.SemiBold)
        }
        Text(
            "Your sign-in is kept encrypted on this phone and only ever sent to your server.",
            color = Volt.Ink3,
            fontSize = 12.5.sp,
            textAlign = TextAlign.Center,
        )
    }
}

@Composable
fun Logo() {
    val shape = RoundedCornerShape(18.dp)
    Box(
        Modifier.size(64.dp).clip(shape).background(Volt.AccentWash).border(1.dp, Volt.AccentLine, shape),
        contentAlignment = Alignment.Center,
    ) {
        Icon(Icons.Outlined.Bolt, contentDescription = null, tint = Volt.Accent, modifier = Modifier.size(34.dp))
    }
}

@Composable
private fun Field(
    label: String,
    value: String,
    onChange: (String) -> Unit,
    placeholder: String,
    keyboard: KeyboardType,
    action: ImeAction,
    secret: Boolean = false,
    onDone: () -> Unit = {},
) {
    OutlinedTextField(
        value = value,
        onValueChange = onChange,
        label = { Text(label) },
        placeholder = { if (placeholder.isNotEmpty()) Text(placeholder, color = Volt.Ink4) },
        singleLine = true,
        visualTransformation = if (secret) PasswordVisualTransformation() else androidx.compose.ui.text.input.VisualTransformation.None,
        keyboardOptions = KeyboardOptions(keyboardType = keyboard, imeAction = action, autoCorrectEnabled = false),
        keyboardActions = KeyboardActions(onDone = { onDone() }),
        shape = RoundedCornerShape(14.dp),
        colors = OutlinedTextFieldDefaults.colors(
            focusedBorderColor = Volt.Accent,
            unfocusedBorderColor = Volt.Line,
            focusedLabelColor = Volt.Accent,
            unfocusedLabelColor = Volt.Ink3,
            cursorColor = Volt.Accent,
            focusedContainerColor = Volt.Elev,
            unfocusedContainerColor = Volt.Elev,
        ),
        modifier = Modifier.fillMaxWidth(),
    )
}
