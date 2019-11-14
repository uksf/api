// ReSharper disable All
/*
 *  Managed C# wrapper for GameNetworkingSockets library by Valve Software
 *  Copyright (c) 2018 Stanislav Denisov
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a copy
 *  of this software and associated documentation files (the "Software"), to deal
 *  in the Software without restriction, including without limitation the rights
 *  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 *  copies of the Software, and to permit persons to whom the Software is
 *  furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in all
 *  copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 *  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 *  SOFTWARE.
 */

#define VALVESOCKETS_SPAN

using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace Valve.Sockets {
    using ListenSocket = UInt32;
    using Connection = UInt32;
    using Microseconds = Int64;

    [Flags]
    public enum SendType {
        UNRELIABLE = 0,
        NO_NAGLE = 1,
        NO_DELAY = 1 << 2,
        RELIABLE = 1 << 3
    }

    public enum IdentityType {
        INVALID = 0,
        IP_ADDRESS = 1,
        GENERIC_STRING = 2,
        GENERIC_BYTES = 3,
        STEAM_ID = 16
    }

    public enum ConnectionState {
        NONE = 0,
        CONNECTING = 1,
        FINDING_ROUTE = 2,
        CONNECTED = 3,
        CLOSED_BY_PEER = 4,
        PROBLEM_DETECTED_LOCALLY = 5
    }

    public enum ConfigurationScope {
        GLOBAL = 1,
        SOCKETS_INTERFACE = 2,
        LISTEN_SOCKET = 3,
        CONNECTION = 4
    }

    public enum ConfigurationDataType {
        INT32 = 1,
        INT64 = 2,
        FLOAT = 3,
        STRING = 4,
        FUNCTION_PTR = 5
    }

    public enum ConfigurationValue {
        INVALID = 0,
        FAKE_PACKET_LOSS_SEND = 2,
        FAKE_PACKET_LOSS_RECV = 3,
        FAKE_PACKET_LAG_SEND = 4,
        FAKE_PACKET_LAG_RECV = 5,
        FAKE_PACKET_REORDER_SEND = 6,
        FAKE_PACKET_REORDER_RECV = 7,
        FAKE_PACKET_REORDER_TIME = 8,
        FAKE_PACKET_DUP_SEND = 26,
        FAKE_PACKET_DUP_RECV = 27,
        FAKE_PACKET_DUP_TIME_MAX = 28,
        TIMEOUT_INITIAL = 24,
        TIMEOUT_CONNECTED = 25,
        SEND_BUFFER_SIZE = 9,
        SEND_RATE_MIN = 10,
        SEND_RATE_MAX = 11,
        NAGLE_TIME = 12,
        IP_ALLOW_WITHOUT_AUTH = 23,
        SDR_CLIENT_CONSECUTITIVE_PING_TIMEOUTS_FAIL_INITIAL = 19,
        SDR_CLIENT_CONSECUTITIVE_PING_TIMEOUTS_FAIL = 20,
        SDR_CLIENT_MIN_PINGS_BEFORE_PING_ACCURATE = 21,
        SDR_CLIENT_SINGLE_SOCKET = 22,
        SDR_CLIENT_FORCE_RELAY_CLUSTER = 29,
        SDR_CLIENT_DEBUG_TICKET_ADDRESS = 30,
        SDR_CLIENT_FORCE_PROXY_ADDR = 31,
        LOG_LEVEL_ACK_RTT = 13,
        LOG_LEVEL_PACKET_DECODE = 14,
        LOG_LEVEL_MESSAGE = 15,
        LOG_LEVEL_PACKET_GAPS = 16,
        LOG_LEVEL_P2_P_RENDEZVOUS = 17,
        LOG_LEVEL_SDR_RELAY_PINGS = 18
    }

    public enum ConfigurationValueResult {
        BAD_VALUE = -1,
        BAD_SCOPE_OBJECT = -2,
        BUFFER_TOO_SMALL = -3,
        OK = 1,
        OK_INHERITED = 2
    }

    public enum DebugType {
        NONE = 0,
        BUG = 1,
        ERROR = 2,
        IMPORTANT = 3,
        WARNING = 4,
        MESSAGE = 5,
        VERBOSE = 6,
        DEBUG = 7,
        EVERYTHING = 8
    }

    public enum Result {
        OK = 1,
        FAIL = 2,
        NO_CONNECTION = 3,
        INVALID_PASSWORD = 5,
        LOGGED_IN_ELSEWHERE = 6,
        INVALID_PROTOCOL_VER = 7,
        INVALID_PARAM = 8,
        FILE_NOT_FOUND = 9,
        BUSY = 10,
        INVALID_STATE = 11,
        INVALID_NAME = 12,
        INVALID_EMAIL = 13,
        DUPLICATE_NAME = 14,
        ACCESS_DENIED = 15,
        TIMEOUT = 16,
        BANNED = 17,
        ACCOUNT_NOT_FOUND = 18,
        INVALID_STEAM_ID = 19,
        SERVICE_UNAVAILABLE = 20,
        NOT_LOGGED_ON = 21,
        PENDING = 22,
        ENCRYPTION_FAILURE = 23,
        INSUFFICIENT_PRIVILEGE = 24,
        LIMIT_EXCEEDED = 25,
        REVOKED = 26,
        EXPIRED = 27,
        ALREADY_REDEEMED = 28,
        DUPLICATE_REQUEST = 29,
        ALREADY_OWNED = 30,
        IP_NOT_FOUND = 31,
        PERSIST_FAILED = 32,
        LOCKING_FAILED = 33,
        LOGON_SESSION_REPLACED = 34,
        CONNECT_FAILED = 35,
        HANDSHAKE_FAILED = 36,
        IO_FAILURE = 37,
        REMOTE_DISCONNECT = 38,
        SHOPPING_CART_NOT_FOUND = 39,
        BLOCKED = 40,
        IGNORED = 41,
        NO_MATCH = 42,
        ACCOUNT_DISABLED = 43,
        SERVICE_READ_ONLY = 44,
        ACCOUNT_NOT_FEATURED = 45,
        ADMINISTRATOR_OK = 46,
        CONTENT_VERSION = 47,
        TRY_ANOTHER_CM = 48,
        PASSWORD_REQUIRED_TO_KICK_SESSION = 49,
        ALREADY_LOGGED_IN_ELSEWHERE = 50,
        SUSPENDED = 51,
        CANCELLED = 52,
        DATA_CORRUPTION = 53,
        DISK_FULL = 54,
        REMOTE_CALL_FAILED = 55,
        PASSWORD_UNSET = 56,
        EXTERNAL_ACCOUNT_UNLINKED = 57,
        PSN_TICKET_INVALID = 58,
        EXTERNAL_ACCOUNT_ALREADY_LINKED = 59,
        REMOTE_FILE_CONFLICT = 60,
        ILLEGAL_PASSWORD = 61,
        SAME_AS_PREVIOUS_VALUE = 62,
        ACCOUNT_LOGON_DENIED = 63,
        CANNOT_USE_OLD_PASSWORD = 64,
        INVALID_LOGIN_AUTH_CODE = 65,
        ACCOUNT_LOGON_DENIED_NO_MAIL = 66,
        HARDWARE_NOT_CAPABLE_OF_IPT = 67,
        IPT_INIT_ERROR = 68,
        PARENTAL_CONTROL_RESTRICTED = 69,
        FACEBOOK_QUERY_ERROR = 70,
        EXPIRED_LOGIN_AUTH_CODE = 71,
        IP_LOGIN_RESTRICTION_FAILED = 72,
        ACCOUNT_LOCKED_DOWN = 73,
        ACCOUNT_LOGON_DENIED_VERIFIED_EMAIL_REQUIRED = 74,
        NO_MATCHING_URL = 75,
        BAD_RESPONSE = 76,
        REQUIRE_PASSWORD_RE_ENTRY = 77,
        VALUE_OUT_OF_RANGE = 78,
        UNEXPECTED_ERROR = 79,
        DISABLED = 80,
        INVALID_CEG_SUBMISSION = 81,
        RESTRICTED_DEVICE = 82,
        REGION_LOCKED = 83,
        RATE_LIMIT_EXCEEDED = 84,
        ACCOUNT_LOGIN_DENIED_NEED_TWO_FACTOR = 85,
        ITEM_DELETED = 86,
        ACCOUNT_LOGIN_DENIED_THROTTLE = 87,
        TWO_FACTOR_CODE_MISMATCH = 88,
        TWO_FACTOR_ACTIVATION_CODE_MISMATCH = 89,
        ACCOUNT_ASSOCIATED_TO_MULTIPLE_PARTNERS = 90,
        NOT_MODIFIED = 91,
        NO_MOBILE_DEVICE = 92,
        TIME_NOT_SYNCED = 93,
        SMS_CODE_FAILED = 94,
        ACCOUNT_LIMIT_EXCEEDED = 95,
        ACCOUNT_ACTIVITY_LIMIT_EXCEEDED = 96,
        PHONE_ACTIVITY_LIMIT_EXCEEDED = 97,
        REFUND_TO_WALLET = 98,
        EMAIL_SEND_FAILURE = 99,
        NOT_SETTLED = 100,
        NEED_CAPTCHA = 101,
        GSLT_DENIED = 102,
        GS_OWNER_DENIED = 103,
        INVALID_ITEM_TYPE = 104,
        IP_BANNED = 105,
        GSLT_EXPIRED = 106,
        INSUFFICIENT_FUNDS = 107,
        TOO_MANY_PENDING = 108,
        NO_SITE_LICENSES_FOUND = 109,
        WG_NETWORK_SEND_EXCEEDED = 110
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Address {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public byte[] ip;

        public ushort port;

        public bool IsLocalHost => Native.SteamAPI_SteamNetworkingIPAddr_IsLocalHost(ref this);

        public string GetIp() => ip.ParseIp();

        public void SetLocalHost(ushort port) {
            Native.SteamAPI_SteamNetworkingIPAddr_SetIPv6LocalHost(ref this, port);
        }

        public void SetAddress(string ip, ushort port) {
            if (!ip.Contains(":")) {
                Native.SteamAPI_SteamNetworkingIPAddr_SetIPv4(ref this, ip.ParseIPv4(), port);
            } else {
                Native.SteamAPI_SteamNetworkingIPAddr_SetIPv6(ref this, ip.ParseIPv6(), port);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct StatusInfo {
        private const int CALLBACK = Library.SOCKETS_CALLBACKS + 1;
        public uint connection;
        public ConnectionInfo connectionInfo;
        private readonly int socketState;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ConnectionInfo {
        public NetworkingIdentity identity;
        public long userData;
        public uint listenSocket;
        public Address address;
        private readonly ushort pad;
        private readonly uint popRemote;
        private readonly uint popRelay;
        public ConnectionState state;
        public int endReason;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string endDebug;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string connectionDescription;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ConnectionStatus {
        public ConnectionState state;
        public int ping;
        public float connectionQualityLocal;
        public float connectionQualityRemote;
        public float outPacketsPerSecond;
        public float outBytesPerSecond;
        public float inPacketsPerSecond;
        public float inBytesPerSecond;
        public int sendRateBytesPerSecond;
        public int pendingUnreliable;
        public int pendingReliable;
        public int sentUnackedReliable;
        public long queueTime;
    }

    [StructLayout(LayoutKind.Explicit, Size = 136)]
    public struct NetworkingIdentity {
        [FieldOffset(0)] public IdentityType type;

        public bool IsInvalid => Native.SteamAPI_SteamNetworkingIdentity_IsInvalid(ref this);

        public ulong GetSteamId() => Native.SteamAPI_SteamNetworkingIdentity_GetSteamID64(ref this);

        public void SetSteamId(ulong steamId) {
            Native.SteamAPI_SteamNetworkingIdentity_SetSteamID64(ref this, steamId);
        }

        public bool EqualsTo(ref NetworkingIdentity identity) => Native.SteamAPI_SteamNetworkingIdentity_EqualTo(ref this, ref identity);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NetworkingMessage {
        public IntPtr data;
        public int length;
        public uint connection;
        public NetworkingIdentity identity;
        public long userData;
        public long timeReceived;
        public long messageNumber;
        internal IntPtr release;
        public int channel;
        private readonly int pad;

        public readonly void CopyTo(byte[] destination) {
            if (destination == null) {
                throw new ArgumentNullException("destination");
            }

            Marshal.Copy(data, destination, 0, length);
        }

#if !VALVESOCKETS_SPAN
        public void Destroy() {
            if (release == IntPtr.Zero) {
                throw new InvalidOperationException("Message not created");
            }

            Native.SteamAPI_SteamNetworkingMessage_t_Release(release);
        }
#endif
    }

    public delegate void StatusCallback(StatusInfo info, IntPtr context);

    public delegate void DebugCallback(DebugType type, string message);

#if VALVESOCKETS_SPAN
    public delegate void MessageCallback(in NetworkingMessage message);
#endif

    internal static class ArrayPool {
        [ThreadStatic] private static IntPtr[] pointerBuffer;

        public static IntPtr[] GetPointerBuffer() {
            if (pointerBuffer == null) {
                pointerBuffer = new IntPtr[Library.MAX_MESSAGES_PER_BATCH];
            }

            return pointerBuffer;
        }
    }

    public class NetworkingSockets {
        private readonly IntPtr _nativeSockets;
        private readonly int _nativeMessageSize = Marshal.SizeOf(typeof(NetworkingMessage));

        public NetworkingSockets() {
            _nativeSockets = Native.SteamNetworkingSockets();

            if (_nativeSockets == IntPtr.Zero) {
                throw new InvalidOperationException("Networking sockets not created");
            }
        }

        public uint CreateListenSocket(ref Address address) => Native.SteamAPI_ISteamNetworkingSockets_CreateListenSocketIP(_nativeSockets, ref address);

        public uint Connect(ref Address address) => Native.SteamAPI_ISteamNetworkingSockets_ConnectByIPAddress(_nativeSockets, ref address);

        public Result AcceptConnection(uint connection) => Native.SteamAPI_ISteamNetworkingSockets_AcceptConnection(_nativeSockets, connection);

        public bool CloseConnection(uint connection) => CloseConnection(connection, 0, string.Empty, false);

        public bool CloseConnection(uint connection, int reason, string debug, bool enableLinger) {
            if (reason > Library.MAX_CLOSE_REASON_VALUE) {
                throw new ArgumentOutOfRangeException("reason");
            }

            if (debug.Length > Library.MAX_CLOSE_MESSAGE_LENGTH) {
                throw new ArgumentOutOfRangeException("debug");
            }

            return Native.SteamAPI_ISteamNetworkingSockets_CloseConnection(_nativeSockets, connection, reason, debug, enableLinger);
        }

        public bool CloseListenSocket(uint socket) => Native.SteamAPI_ISteamNetworkingSockets_CloseListenSocket(_nativeSockets, socket);

        public bool SetConnectionUserData(uint peer, long userData) => Native.SteamAPI_ISteamNetworkingSockets_SetConnectionUserData(_nativeSockets, peer, userData);

        public long GetConnectionUserData(uint peer) => Native.SteamAPI_ISteamNetworkingSockets_GetConnectionUserData(_nativeSockets, peer);

        public void SetConnectionName(uint peer, string name) {
            Native.SteamAPI_ISteamNetworkingSockets_SetConnectionName(_nativeSockets, peer, name);
        }

        public bool GetConnectionName(uint peer, StringBuilder name, int maxLength) => Native.SteamAPI_ISteamNetworkingSockets_GetConnectionName(_nativeSockets, peer, name, maxLength);

        public Result SendMessageToConnection(uint connection, IntPtr data, uint length) => SendMessageToConnection(connection, data, length, SendType.UNRELIABLE);

        public Result SendMessageToConnection(uint connection, IntPtr data, uint length, SendType flags) => SendMessageToConnection(connection, data, length, flags);

        public Result SendMessageToConnection(uint connection, IntPtr data, int length, SendType flags) => Native.SteamAPI_ISteamNetworkingSockets_SendMessageToConnection(_nativeSockets, connection, data, (uint) length, flags);

        public Result SendMessageToConnection(uint connection, byte[] data) => SendMessageToConnection(connection, data, data.Length, SendType.UNRELIABLE);

        public Result SendMessageToConnection(uint connection, byte[] data, SendType flags) => SendMessageToConnection(connection, data, data.Length, flags);

        public Result SendMessageToConnection(uint connection, byte[] data, int length, SendType flags) => Native.SteamAPI_ISteamNetworkingSockets_SendMessageToConnection(_nativeSockets, connection, data, (uint) length, flags);

        public Result FlushMessagesOnConnection(uint connection) => Native.SteamAPI_ISteamNetworkingSockets_FlushMessagesOnConnection(_nativeSockets, connection);

#if VALVESOCKETS_SPAN
#if VALVESOCKETS_INLINING
				[MethodImpl(256)]
#endif
        public void ReceiveMessagesOnConnection(Connection connection, MessageCallback callback, int maxMessages) {
            if (maxMessages > Library.MAX_MESSAGES_PER_BATCH) throw new ArgumentOutOfRangeException("maxMessages");

            IntPtr[] nativeMessages = ArrayPool.GetPointerBuffer();
            int messagesCount = Native.SteamAPI_ISteamNetworkingSockets_ReceiveMessagesOnConnection(_nativeSockets, connection, nativeMessages, maxMessages);

            for (int i = 0; i < messagesCount; i++) {
                Span<NetworkingMessage> message;

                unsafe {
                    message = new Span<NetworkingMessage>((void*) nativeMessages[i], 1);
                }

                callback(in message[0]);

                Native.SteamAPI_SteamNetworkingMessage_t_Release(nativeMessages[i]);
            }
        }

#if VALVESOCKETS_INLINING
				[MethodImpl(256)]
#endif
        public void ReceiveMessagesOnListenSocket(ListenSocket socket, MessageCallback callback, int maxMessages) {
            if (maxMessages > Library.MAX_MESSAGES_PER_BATCH) throw new ArgumentOutOfRangeException("maxMessages");

            IntPtr[] nativeMessages = ArrayPool.GetPointerBuffer();
            int messagesCount = Native.SteamAPI_ISteamNetworkingSockets_ReceiveMessagesOnListenSocket(_nativeSockets, socket, nativeMessages, maxMessages);

            for (int i = 0; i < messagesCount; i++) {
                Span<NetworkingMessage> message;

                unsafe {
                    message = new Span<NetworkingMessage>((void*) nativeMessages[i], 1);
                }

                callback(in message[0]);

                Native.SteamAPI_SteamNetworkingMessage_t_Release(nativeMessages[i]);
            }
        }
#else
#if VALVESOCKETS_INLINING
				[MethodImpl(256)]
#endif
        public int ReceiveMessagesOnConnection(uint connection, NetworkingMessage[] messages, int maxMessages) {
            if (maxMessages > Library.MAX_MESSAGES_PER_BATCH) {
                throw new ArgumentOutOfRangeException("maxMessages");
            }

            IntPtr[] nativeMessages = ArrayPool.GetPointerBuffer();
            int messagesCount = Native.SteamAPI_ISteamNetworkingSockets_ReceiveMessagesOnConnection(_nativeSockets, connection, nativeMessages, maxMessages);

            for (int i = 0; i < messagesCount; i++) {
                messages[i] = (NetworkingMessage) Marshal.PtrToStructure(nativeMessages[i], typeof(NetworkingMessage));
                messages[i].release = nativeMessages[i];
            }

            return messagesCount;
        }

#if VALVESOCKETS_INLINING
				[MethodImpl(256)]
#endif
        public int ReceiveMessagesOnListenSocket(uint socket, NetworkingMessage[] messages, int maxMessages) {
            if (maxMessages > Library.MAX_MESSAGES_PER_BATCH) {
                throw new ArgumentOutOfRangeException("maxMessages");
            }

            IntPtr[] nativeMessages = ArrayPool.GetPointerBuffer();
            int messagesCount = Native.SteamAPI_ISteamNetworkingSockets_ReceiveMessagesOnListenSocket(_nativeSockets, socket, nativeMessages, maxMessages);

            for (int i = 0; i < messagesCount; i++) {
                messages[i] = (NetworkingMessage) Marshal.PtrToStructure(nativeMessages[i], typeof(NetworkingMessage));
                messages[i].release = nativeMessages[i];
            }

            return messagesCount;
        }
#endif

        public bool GetConnectionInfo(uint connection, ref ConnectionInfo info) => Native.SteamAPI_ISteamNetworkingSockets_GetConnectionInfo(_nativeSockets, connection, ref info);

        public bool GetQuickConnectionStatus(uint connection, ConnectionStatus status) => Native.SteamAPI_ISteamNetworkingSockets_GetQuickConnectionStatus(_nativeSockets, connection, status);

        public int GetDetailedConnectionStatus(uint connection, StringBuilder status, int statusLength) => Native.SteamAPI_ISteamNetworkingSockets_GetDetailedConnectionStatus(_nativeSockets, connection, status, statusLength);

        public bool GetListenSocketAddress(uint socket, ref Address address) => Native.SteamAPI_ISteamNetworkingSockets_GetListenSocketAddress(_nativeSockets, socket, ref address);

        public bool CreateSocketPair(uint connectionOne, uint connectionTwo, bool useNetworkLoopback, NetworkingIdentity identityOne, NetworkingIdentity identityTwo) =>
            Native.SteamAPI_ISteamNetworkingSockets_CreateSocketPair(_nativeSockets, connectionOne, connectionTwo, useNetworkLoopback, identityOne, identityTwo);

        public void DispatchCallback(StatusCallback callback) {
            DispatchCallback(callback, IntPtr.Zero);
        }

        public void DispatchCallback(StatusCallback callback, IntPtr context) {
            Native.SteamAPI_ISteamNetworkingSockets_RunConnectionStatusChangedCallbacks(_nativeSockets, callback, context);
        }
    }

    public class NetworkingUtils {
        private readonly IntPtr _nativeUtils;

        public NetworkingUtils() {
            _nativeUtils = Native.SteamNetworkingUtils();

            if (_nativeUtils == IntPtr.Zero) {
                throw new InvalidOperationException("Networking utils not created");
            }
        }

        public ConfigurationValue FirstConfigurationValue => Native.SteamAPI_ISteamNetworkingUtils_GetFirstConfigValue(_nativeUtils);

        public long Time => Native.SteamAPI_ISteamNetworkingUtils_GetLocalTimestamp(_nativeUtils);

        public void SetDebugCallback(DebugType detailLevel, DebugCallback callback) {
            Native.SteamAPI_ISteamNetworkingUtils_SetDebugOutputFunction(_nativeUtils, detailLevel, callback);
        }

        public bool SetConfiguratioValue(ConfigurationValue configurationValue, ConfigurationScope configurationScope, IntPtr scopeObject, ConfigurationDataType dataType, IntPtr value) =>
            Native.SteamAPI_ISteamNetworkingUtils_SetConfigValue(_nativeUtils, configurationValue, configurationScope, scopeObject, dataType, value);

        public ConfigurationValueResult GetConfigurationValue(ConfigurationValue configurationValue, ConfigurationScope configurationScope, IntPtr scopeObject, out ConfigurationDataType dataType, out IntPtr result, out IntPtr resultLength) =>
            Native.SteamAPI_ISteamNetworkingUtils_GetConfigValue(_nativeUtils, configurationValue, configurationScope, scopeObject, out dataType, out result, out resultLength);
    }

    public static class Extensions {
        public static uint ParseIPv4(this string ip) {
            IPAddress address = default;

            if (IPAddress.TryParse(ip, out address)) {
                if (address.AddressFamily != AddressFamily.InterNetwork) {
                    throw new Exception("Incorrect format of an IPv4 address");
                }
            }

            byte[] bytes = address.GetAddressBytes();

            Array.Reverse(bytes);

            return BitConverter.ToUInt32(bytes, 0);
        }

        public static byte[] ParseIPv6(this string ip) {
            IPAddress address = default;

            if (IPAddress.TryParse(ip, out address)) {
                if (address.AddressFamily != AddressFamily.InterNetworkV6) {
                    throw new Exception("Incorrect format of an IPv6 address");
                }
            }

            return address.GetAddressBytes();
        }

        public static string ParseIp(this byte[] ip) {
            IPAddress address = new IPAddress(ip);
            string converted = address.ToString();

            if (converted.Length > 7 && converted.Remove(7) == "::ffff:") {
                Address ipv4 = default;

                ipv4.ip = ip;

                byte[] bytes = BitConverter.GetBytes(Native.SteamAPI_SteamNetworkingIPAddr_GetIPv4(ref ipv4));

                Array.Reverse(bytes);

                address = new IPAddress(bytes);
            }

            return address.ToString();
        }
    }

    public static class Library {
        public const int MAX_CLOSE_MESSAGE_LENGTH = 128;
        public const int MAX_CLOSE_REASON_VALUE = 999;
        public const int MAX_ERROR_MESSAGE_LENGTH = 1024;
        public const int MAX_MESSAGE_SIZE = 512 * 1024;
        public const int MAX_MESSAGES_PER_BATCH = 256;
        public const int SOCKETS_CALLBACKS = 1220;

        public static bool Initialize() => Initialize(null);

        public static bool Initialize(StringBuilder errorMessage) {
            if (errorMessage != null && errorMessage.Capacity != MAX_ERROR_MESSAGE_LENGTH) {
                throw new ArgumentOutOfRangeException("Capacity of the error message must be equal to " + MAX_ERROR_MESSAGE_LENGTH);
            }

            return Native.GameNetworkingSockets_Init(IntPtr.Zero, errorMessage);
        }

        public static void Deinitialize() {
            Native.GameNetworkingSockets_Kill();
        }
    }

    [SuppressUnmanagedCodeSecurity]
    internal static class Native {
        private const string NATIVE_LIBRARY = "GameNetworkingSockets";

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool GameNetworkingSockets_Init(IntPtr identity, StringBuilder errorMessage);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void GameNetworkingSockets_Kill();

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr SteamNetworkingSockets();

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr SteamNetworkingUtils();

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint SteamAPI_ISteamNetworkingSockets_CreateListenSocketIP(IntPtr sockets, ref Address address);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint SteamAPI_ISteamNetworkingSockets_ConnectByIPAddress(IntPtr sockets, ref Address address);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern Result SteamAPI_ISteamNetworkingSockets_AcceptConnection(IntPtr sockets, uint connection);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SteamAPI_ISteamNetworkingSockets_CloseConnection(IntPtr sockets, uint peer, int reason, string debug, bool enableLinger);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SteamAPI_ISteamNetworkingSockets_CloseListenSocket(IntPtr sockets, uint socket);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SteamAPI_ISteamNetworkingSockets_SetConnectionUserData(IntPtr sockets, uint peer, long userData);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern long SteamAPI_ISteamNetworkingSockets_GetConnectionUserData(IntPtr sockets, uint peer);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SteamAPI_ISteamNetworkingSockets_SetConnectionName(IntPtr sockets, uint peer, string name);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SteamAPI_ISteamNetworkingSockets_GetConnectionName(IntPtr sockets, uint peer, StringBuilder name, int maxLength);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern Result SteamAPI_ISteamNetworkingSockets_SendMessageToConnection(IntPtr sockets, uint connection, IntPtr data, uint length, SendType flags);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern Result SteamAPI_ISteamNetworkingSockets_SendMessageToConnection(IntPtr sockets, uint connection, byte[] data, uint length, SendType flags);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern Result SteamAPI_ISteamNetworkingSockets_FlushMessagesOnConnection(IntPtr sockets, uint connection);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SteamAPI_ISteamNetworkingSockets_ReceiveMessagesOnConnection(IntPtr sockets, uint connection, IntPtr[] messages, int maxMessages);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SteamAPI_ISteamNetworkingSockets_ReceiveMessagesOnListenSocket(IntPtr sockets, uint socket, IntPtr[] messages, int maxMessages);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SteamAPI_ISteamNetworkingSockets_GetConnectionInfo(IntPtr sockets, uint connection, ref ConnectionInfo info);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SteamAPI_ISteamNetworkingSockets_GetQuickConnectionStatus(IntPtr sockets, uint connection, ConnectionStatus status);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SteamAPI_ISteamNetworkingSockets_GetDetailedConnectionStatus(IntPtr sockets, uint connection, StringBuilder status, int statusLength);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SteamAPI_ISteamNetworkingSockets_GetListenSocketAddress(IntPtr sockets, uint socket, ref Address address);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SteamAPI_ISteamNetworkingSockets_RunConnectionStatusChangedCallbacks(IntPtr sockets, StatusCallback callback, IntPtr context);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SteamAPI_ISteamNetworkingSockets_CreateSocketPair(IntPtr sockets, uint connectionOne, uint connectionTwo, bool useNetworkLoopback, NetworkingIdentity identityOne, NetworkingIdentity identityTwo);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SteamAPI_SteamNetworkingIPAddr_SetIPv6(ref Address address, byte[] ip, ushort port);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SteamAPI_SteamNetworkingIPAddr_SetIPv4(ref Address address, uint ip, ushort port);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint SteamAPI_SteamNetworkingIPAddr_GetIPv4(ref Address address);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SteamAPI_SteamNetworkingIPAddr_SetIPv6LocalHost(ref Address address, ushort port);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SteamAPI_SteamNetworkingIPAddr_IsLocalHost(ref Address address);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SteamAPI_SteamNetworkingIdentity_IsInvalid(ref NetworkingIdentity identity);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SteamAPI_SteamNetworkingIdentity_SetSteamID64(ref NetworkingIdentity identity, ulong steamId);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ulong SteamAPI_SteamNetworkingIdentity_GetSteamID64(ref NetworkingIdentity identity);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SteamAPI_SteamNetworkingIdentity_EqualTo(ref NetworkingIdentity identityOne, ref NetworkingIdentity identityTwo);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern long SteamAPI_ISteamNetworkingUtils_GetLocalTimestamp(IntPtr utils);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SteamAPI_ISteamNetworkingUtils_SetDebugOutputFunction(IntPtr utils, DebugType detailLevel, DebugCallback callback);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool SteamAPI_ISteamNetworkingUtils_SetConfigValue(IntPtr utils, ConfigurationValue configurationValue, ConfigurationScope configurationScope, IntPtr scopeObject, ConfigurationDataType dataType, IntPtr value);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ConfigurationValueResult SteamAPI_ISteamNetworkingUtils_GetConfigValue(IntPtr utils, ConfigurationValue configurationValue, ConfigurationScope configurationScope, IntPtr scopeObject, out ConfigurationDataType dataType, out IntPtr result, out IntPtr resultLength);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ConfigurationValue SteamAPI_ISteamNetworkingUtils_GetFirstConfigValue(IntPtr utils);

        [DllImport(NATIVE_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SteamAPI_SteamNetworkingMessage_t_Release(IntPtr nativeMessage);
    }
}
