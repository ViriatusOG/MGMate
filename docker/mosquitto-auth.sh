#!/bin/sh
# Writes the Mosquitto password and ACL files into the directory given as $1. Shared by the
# mosquitto service in docker-compose.yml and the all-in-one image's entrypoint, and run on
# every start so changed credentials always take effect.
#
#   MQTT_BROKER_USERNAME / MQTT_BROKER_PASSWORD  Internal login for the SAIC gateway, Api and
#                                                Worker. Full access. The only login.
set -eu

dir="${1:?Usage: mosquitto-auth.sh <directory>}"
passwd_file="$dir/passwd"
acl_file="$dir/acl"

: "${MQTT_BROKER_USERNAME:?MQTT_BROKER_USERNAME is required}"
: "${MQTT_BROKER_PASSWORD:?MQTT_BROKER_PASSWORD is required}"

rm -f "$passwd_file" "$acl_file"
mosquitto_passwd -b -c "$passwd_file" "$MQTT_BROKER_USERNAME" "$MQTT_BROKER_PASSWORD"
printf 'user %s\ntopic readwrite #\n' "$MQTT_BROKER_USERNAME" > "$acl_file"

# Mosquitto 2.x refuses to load these files unless they are owned by the "mosquitto" account
chown mosquitto:mosquitto "$passwd_file" "$acl_file"
chmod 600 "$passwd_file" "$acl_file"
