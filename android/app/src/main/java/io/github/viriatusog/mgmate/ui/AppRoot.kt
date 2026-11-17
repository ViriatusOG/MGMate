package io.github.viriatusog.mgmate.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.systemBarsPadding
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Bolt
import androidx.compose.material.icons.outlined.DirectionsCar
import androidx.compose.material.icons.outlined.Fingerprint
import androidx.compose.material.icons.outlined.Route
import androidx.compose.material.icons.outlined.Settings
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.NavigationBarItemDefaults
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Snackbar
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.pulltorefresh.PullToRefreshBox
import androidx.compose.material3.pulltorefresh.PullToRefreshDefaults
import androidx.compose.material3.pulltorefresh.rememberPullToRefreshState
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import io.github.viriatusog.mgmate.AppViewModel
import io.github.viriatusog.mgmate.data.Units
import kotlinx.coroutines.delay
import java.time.Instant

private enum class Tab(val label: String, val icon: ImageVector) {
    Car("Car", Icons.Outlined.DirectionsCar),
    Charging("Charging", Icons.Outlined.Bolt),
    Trips("Trips", Icons.Outlined.Route),
    Settings("Settings", Icons.Outlined.Settings),
}

/** The whole app: sign-in until signed in, the lock until unlocked, then the four tabs. */
@Composable
fun AppRoot(vm: AppViewModel, locked: Boolean, canLock: Boolean, onUnlock: () -> Unit, onEnableLock: () -> Unit) {
    MGMateTheme {
        when {
            !vm.signedIn -> SignInScreen(vm)
            vm.lockSetup -> LockSetupScreen(canLock, onEnableLock) { vm.chooseLock(false) }
            locked -> LockScreen(onUnlock)
            else -> Tabs(vm, canLock)
        }
    }
}

/** Shown once after signing in: the app asks to be protected before it shows the car. */
@Composable
private fun LockSetupScreen(canLock: Boolean, onEnable: () -> Unit, onSkip: () -> Unit) {
    Column(
        Modifier.fillMaxSize().background(Volt.Bg).systemBarsPadding().padding(24.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp, Alignment.CenterVertically),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Logo()
        Text("Protect the app", color = Volt.Ink, fontSize = 22.sp, fontWeight = FontWeight.SemiBold)
        Text(
            if (canLock) {
                "This app can unlock and start your car. Open it with your fingerprint, face or PIN, so you never type your password again and nobody else can use it."
            } else {
                "This app can unlock and start your car, but this phone has no fingerprint or screen lock to protect it with. Set one up in the phone's settings, then switch on the lock under Settings in this app."
            },
            color = Volt.Ink2,
            fontSize = 15.sp,
            lineHeight = 21.sp,
            textAlign = TextAlign.Center,
        )
        if (canLock) {
            Button(
                onClick = onEnable,
                modifier = Modifier.fillMaxWidth().height(50.dp),
                shape = RoundedCornerShape(14.dp),
                colors = ButtonDefaults.buttonColors(containerColor = Volt.Accent, contentColor = Volt.AccentInk),
            ) {
                Icon(Icons.Outlined.Fingerprint, contentDescription = null, modifier = Modifier.size(20.dp))
                Spacer(Modifier.size(8.dp))
                Text("Use fingerprint or PIN", fontSize = 16.sp, fontWeight = FontWeight.SemiBold)
            }
        }
        TextButton(onClick = onSkip, modifier = Modifier.fillMaxWidth().height(48.dp)) {
            Text(if (canLock) "Not now" else "Continue without a lock", color = Volt.Ink2, fontSize = 16.sp)
        }
    }
}

@Composable
private fun LockScreen(onUnlock: () -> Unit) {
    Column(
        Modifier.fillMaxSize().background(Volt.Bg).systemBarsPadding().padding(24.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp, Alignment.CenterVertically),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Logo()
        Text("MGMate is locked", color = Volt.Ink, fontSize = 20.sp, fontWeight = FontWeight.SemiBold)
        Button(
            onClick = onUnlock,
            modifier = Modifier.height(50.dp),
            shape = RoundedCornerShape(14.dp),
            colors = ButtonDefaults.buttonColors(containerColor = Volt.Accent, contentColor = Volt.AccentInk),
        ) {
            Icon(Icons.Outlined.Fingerprint, contentDescription = null, modifier = Modifier.size(20.dp))
            Spacer(Modifier.size(8.dp))
            Text("Unlock", fontSize = 16.sp, fontWeight = FontWeight.SemiBold)
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun Tabs(vm: AppViewModel, canLock: Boolean) {
    var tab by rememberSaveable { mutableIntStateOf(0) }
    var confirm by remember { mutableStateOf<Confirm?>(null) }
    val snackbar = remember { SnackbarHostState() }
    val pull = rememberPullToRefreshState()

    val note = vm.message
    LaunchedEffect(note) {
        if (note != null) {
            vm.message = null
            snackbar.showSnackbar(note)
        }
    }

    Scaffold(
        containerColor = Volt.Bg,
        topBar = { TopBar(vm) },
        snackbarHost = {
            SnackbarHost(snackbar) { data ->
                Snackbar(data, containerColor = Volt.Raise, contentColor = Volt.Ink, shape = RoundedCornerShape(14.dp))
            }
        },
        bottomBar = {
            Column {
                HorizontalDivider(color = Volt.LineSoft)
                NavigationBar(containerColor = Volt.Bg) {
                    Tab.entries.forEachIndexed { index, item ->
                        NavigationBarItem(
                            selected = tab == index,
                            onClick = {
                                tab = index
                                if (item != Tab.Trips) vm.closeTrip()
                            },
                            icon = { Icon(item.icon, contentDescription = null) },
                            label = { Text(item.label, fontSize = 11.sp, fontWeight = FontWeight.SemiBold) },
                            colors = NavigationBarItemDefaults.colors(
                                selectedIconColor = Volt.Accent,
                                selectedTextColor = Volt.Accent,
                                unselectedIconColor = Volt.Ink4,
                                unselectedTextColor = Volt.Ink4,
                                indicatorColor = Volt.AccentWash,
                            ),
                        )
                    }
                }
            }
        },
    ) { padding ->
        Column(Modifier.padding(padding).fillMaxSize()) {
            vm.loadError?.let { error ->
                Text(
                    error,
                    color = Volt.Alert,
                    fontSize = 13.sp,
                    modifier = Modifier.fillMaxWidth().background(Volt.AlertWash).padding(horizontal = 16.dp, vertical = 8.dp),
                )
            }
            // Pulling down reads the server's newest data again, like the button in the top bar.
            // It does not wake the car.
            PullToRefreshBox(
                isRefreshing = vm.loading,
                onRefresh = vm::refresh,
                modifier = Modifier.fillMaxSize(),
                state = pull,
                indicator = {
                    PullToRefreshDefaults.Indicator(
                        state = pull,
                        isRefreshing = vm.loading,
                        modifier = Modifier.align(Alignment.TopCenter),
                        containerColor = Volt.Raise,
                        color = Volt.Accent,
                    )
                },
            ) {
                when (Tab.entries[tab]) {
                    Tab.Car -> CarScreen(vm) { confirm = it }
                    Tab.Charging -> ChargingScreen(vm) { confirm = it }
                    Tab.Trips -> TripsScreen(vm)
                    Tab.Settings -> SettingsScreen(vm, canLock) { confirm = it }
                }
            }
        }
    }

    confirm?.let { ConfirmSheet(it) { confirm = null } }
}

/** The car's name and how old the data on screen is. Pulling a page down reads the server again. */
@Composable
private fun TopBar(vm: AppViewModel) {
    // Ticks so "2 min ago" keeps counting while the screen stays open.
    var now by remember { mutableStateOf(Instant.now()) }
    LaunchedEffect(Unit) {
        while (true) {
            delay(30_000)
            now = Instant.now()
        }
    }

    Column(Modifier.background(Volt.Bg).statusBarsPadding()) {
        Row(Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 10.dp), verticalAlignment = Alignment.CenterVertically) {
            Column(Modifier.weight(1f)) {
                Text(vm.vehicle?.model ?: "MGMate", color = Volt.Ink, fontSize = 19.sp, fontWeight = FontWeight.SemiBold)
                Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                    Box(Modifier.size(7.dp).clip(CircleShape).background(if (vm.loadError == null) Volt.Info else Volt.Alert))
                    val read = Units.parseInstant(vm.status?.recordedAt)
                    Text(
                        if (read != null) "Car last reported ${Units.time(read)} · ${Units.ago(read, now)}" else "No data from the car yet",
                        color = Volt.Ink3,
                        fontSize = 13.sp,
                    )
                }
            }
        }
        HorizontalDivider(color = Volt.LineSoft)
                NavigationBar(containerColor = Volt.Bg) {
                    Tab.entries.forEachIndexed { index, item ->
                        NavigationBarItem(
                            selected = tab == index,
                            onClick = {
                                tab = index
                                if (item != Tab.Trips) vm.closeTrip()
                            },
                            icon = { Icon(item.icon, contentDescription = null) },
                            label = { Text(item.label, fontSize = 11.sp, fontWeight = FontWeight.SemiBold) },
                            colors = NavigationBarItemDefaults.colors(
                                selectedIconColor = Volt.Accent,
                                selectedTextColor = Volt.Accent,
                                unselectedIconColor = Volt.Ink4,
                                unselectedTextColor = Volt.Ink4,
                                indicatorColor = Volt.AccentWash,
                            ),
                        )
                    }
                }
            }
        },
    ) { padding ->
        Column(Modifier.padding(padding).fillMaxSize()) {
            vm.loadError?.let { error ->
                Text(
                    error,
                    color = Volt.Alert,
                    fontSize = 13.sp,
                    modifier = Modifier.fillMaxWidth().background(Volt.AlertWash).padding(horizontal = 16.dp, vertical = 8.dp),
                )
            }
            // Pulling down reads the server's newest data again, like the button in the top bar.
            // It does not wake the car.
            PullToRefreshBox(
                isRefreshing = vm.loading,
                onRefresh = vm::refresh,
                modifier = Modifier.fillMaxSize(),
                state = pull,
                indicator = {
                    PullToRefreshDefaults.Indicator(
                        state = pull,
                        isRefreshing = vm.loading,
                        modifier = Modifier.align(Alignment.TopCenter),
                        containerColor = Volt.Raise,
                        color = Volt.Accent,
                    )
                },
            ) {
                when (Tab.entries[tab]) {
                    Tab.Car -> CarScreen(vm) { confirm = it }
                    Tab.Charging -> ChargingScreen(vm) { confirm = it }
                    Tab.Trips -> TripsScreen(vm)
                    Tab.Settings -> SettingsScreen(vm, canLock) { confirm = it }
                }
            }
        }
    }

    confirm?.let { ConfirmSheet(it) { confirm = null } }
}

/** The car's name and how old the data on screen is. Pulling a page down reads the server again. */
@Composable
private fun TopBar(vm: AppViewModel) {
    // Ticks so "2 min ago" keeps counting while the screen stays open.
    var now by remember { mutableStateOf(Instant.now()) }
    LaunchedEffect(Unit) {
        while (true) {
            delay(30_000)
            now = Instant.now()
        }
    }

    Column(Modifier.background(Volt.Bg).statusBarsPadding()) {
        Row(Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 10.dp), verticalAlignment = Alignment.CenterVertically) {
            Column(Modifier.weight(1f)) {
                Text(vm.vehicle?.model ?: "MGMate", color = Volt.Ink, fontSize = 19.sp, fontWeight = FontWeight.SemiBold)
                Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                    Box(Modifier.size(7.dp).clip(CircleShape).background(if (vm.loadError == null) Volt.Info else Volt.Alert))
                    val read = Units.parseInstant(vm.status?.recordedAt)
                    Text(
                        if (read != null) "Car last reported ${Units.time(read)} · ${Units.ago(read, now)}" else "No data from the car yet",
                        color = Volt.Ink3,
                        fontSize = 13.sp,
                    )
                }
            }
            IconButton(onClick = vm::refresh, enabled = !vm.loading) {
                if (vm.loading) {
                    CircularProgressIndicator(Modifier.size(20.dp), color = Volt.Ink2, strokeWidth = 2.dp)
                } else {
                    Icon(Icons.Outlined.Refresh, contentDescription = "Read the newest data from your server", tint = Volt.Ink2)
                }
            }
        }
        HorizontalDivider(color = Volt.LineSoft)
    }
}
