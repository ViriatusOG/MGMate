# kotlinx.serialization: keep the generated serializers of the API models.
-keepattributes *Annotation*, InnerClasses
-dontnote kotlinx.serialization.**
-keepclassmembers class io.github.viriatusog.mgmate.data.** {
    *** Companion;
}
-keepclasseswithmembers class io.github.viriatusog.mgmate.data.** {
    kotlinx.serialization.KSerializer serializer(...);
}
-dontwarn org.osmdroid.**
-dontwarn okhttp3.internal.platform.**
-dontwarn org.conscrypt.**
-dontwarn org.bouncycastle.**
-dontwarn org.openjsse.**
-dontwarn com.google.errorprone.annotations.**
-dontwarn javax.annotation.**

# The saved sign-in (EncryptedSharedPreferences) keeps its key set as a protobuf message that Tink
# reads back by reflection on these fields, so they keep their names.
-keepclassmembers class * extends com.google.crypto.tink.shaded.protobuf.GeneratedMessageLite {
    <fields>;
}
