package io.github.viriatusog.mgmate.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.Logout
import androidx.compose.material.icons.outlined.DirectionsCar
import androidx.compose.material.icons.outlined.Dns
import androidx.compose.material.icons.outlined.Fingerprint
import androidx.compose.material.icons.outlined.Info
import androidx.compose.material.icons.outlined.Person
import androidx.compose.material.icons.outlined.Sync
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import io.github.viriatusog.mgmate.AppViewModel
import io.github.viriatusog.mgmate.BuildConfig

/** The Settings tab: the lock, the connection, and asking the car itself for fresh data. */
@Composable
fun SettingsScreen(vm: AppViewModel, canLock: Boolean, ask: (Confirm) -> Unit) {
    var lock by remember { mutableStateOf(vm.session.lockEnabled) }

    Column(
        Modifier.verticalScroll(rememberScrollState()).padding(horizontal = 16.dp).padding(top = 8.dp, bottom = 16.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        SectionTitle("Security")
        Panel {
            ListRow(
                Icons.Outlined.Fingerprint,
                "Lock the app",
                if (canLock) "Opens with your fingerprint, face or screen lock" else "Set a screen lock on this phone to use this",
                on = lock && canLock,
            ) {
                BusySwitch(lock && canLock, busy = false, enabled = canLock) {
                    lock = it
                    vm.session.lockEnabled = it
                }
            }
        }

        SectionTitle("Connection")
        Panel {
            ListRow(Icons.Outlined.Dns, "Server", vm.session.serverUrl ?: "Not set")
            RowDivider()
            ListRow(Icons.Outlined.Person, "Signed in as", vm.session.username ?: "")
            RowDivider()
            val car = vm.vehicle
            ListRow(Icons.Outlined.DirectionsCar, car?.model ?: "Car", car?.vin?.let { "VIN ending ${it.takeLast(6)}" } ?: "No car found yet")
            RowDivider()
            ListRow(
                Icons.AutoMirrored.Outlined.Logout,
                "Sign out",
                "Forgets the saved sign-in on this phone",
                onClick = {
                    ask(
                        Confirm("Sign out?", "The app forgets your sign-in. You will type it in again next time.", "Sign out", danger = true) {
                            vm.signOut()
                        },
                    )
                },
            )
        }

        SectionTitle("Car")
        Panel {
            ListRow(
                Icons.Outlined.Sync,
                "Ask the car for fresh data",
                "Wakes the car. Not needed for everyday use.",
                onClick = {
                    ask(
                        Confirm(
                            "Wake the car?",
                            "The app already shows the newest data your server has, and the server keeps that up to date without waking the car. Asking the car directly wakes it, which draws on its 12 V battery.",
                            "Wake the car",
                        ) {
                            vm.send("refresh", "refresh", "force", done = "Fresh data is on its way")
                        },
                    )
                },
            )
        }

        Panel {
            ListRow(Icons.Outlined.Info, "MGMate for Android", "Version ${BuildConfig.VERSION_NAME}")
        }
        Text(
            "Opening the app never wakes the car. Only the controls you tap do.",
            color = Volt.Ink3,
            fontSize = 12.5.sp,
            modifier = Modifier.padding(horizontal = 4.dp),
        )
    }
}
