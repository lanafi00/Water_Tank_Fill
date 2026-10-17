using System;
using System.Net.Sockets;

/// <summary>
/// Minimal Modbus TCP client: just the two function codes this project needs.
///   FC 01  Read Coils                 (PLC outputs -> Unity)
///   FC 16  Write Multiple Registers   (Unity sensor values -> PLC)
///
/// Plain C#, no UnityEngine dependency, so it can be unit-tested outside Unity.
/// Not thread-safe: use one instance per thread.
///
/// Frame layout (MBAP header + PDU):
///   [transaction id: 2][protocol id = 0: 2][length: 2][unit id: 1][function code: 1][data...]
/// All multi-byte fields are big-endian.
/// </summary>
public class ModbusTcpClient : IDisposable
{
    readonly string host;
    readonly int port;
    readonly byte unitId;
    readonly int timeoutMs;

    TcpClient client;
    NetworkStream stream;
    ushort transactionId;

    public bool IsConnected => client != null && client.Connected;

    public ModbusTcpClient(string host, int port = 502, byte unitId = 1, int timeoutMs = 500)
    {
        this.host = host;
        this.port = port;
        this.unitId = unitId;
        this.timeoutMs = timeoutMs;
    }

    public void Connect()
    {
        Close();
        client = new TcpClient { NoDelay = true, ReceiveTimeout = timeoutMs, SendTimeout = timeoutMs };
        // Bounded connect so a missing PLC doesn't hang the worker thread for ~20 s.
        var result = client.BeginConnect(host, port, null, null);
        if (!result.AsyncWaitHandle.WaitOne(timeoutMs))
        {
            Close();
            throw new TimeoutException($"Could not connect to {host}:{port} within {timeoutMs} ms");
        }
        client.EndConnect(result);
        stream = client.GetStream();
    }

    public void Close()
    {
        stream?.Dispose();
        client?.Close();
        stream = null;
        client = null;
    }

    public void Dispose() => Close();

    /// <summary>FC 01: read <paramref name="count"/> coils starting at <paramref name="start"/>.</summary>
    public bool[] ReadCoils(ushort start, ushort count)
    {
        byte[] pdu = { 0x01, Hi(start), Lo(start), Hi(count), Lo(count) };
        byte[] resp = Exchange(pdu);

        int byteCount = resp[1];
        if (byteCount != (count + 7) / 8 || resp.Length < 2 + byteCount)
            throw new Exception($"Read Coils: unexpected byte count {byteCount}");

        // Coils are packed LSB-first: coil 0 is bit 0 of the first data byte.
        var coils = new bool[count];
        for (int i = 0; i < count; i++)
            coils[i] = ((resp[2 + i / 8] >> (i % 8)) & 1) == 1;
        return coils;
    }

    /// <summary>FC 16: write consecutive 16-bit holding registers starting at <paramref name="start"/>.</summary>
    public void WriteRegisters(ushort start, short[] values)
    {
        int n = values.Length;
        if (n < 1 || n > 123) throw new ArgumentException("Modbus allows 1-123 registers per write");

        byte[] pdu = new byte[6 + 2 * n];
        pdu[0] = 0x10;
        pdu[1] = Hi(start); pdu[2] = Lo(start);
        pdu[3] = Hi((ushort)n); pdu[4] = Lo((ushort)n);
        pdu[5] = (byte)(2 * n);
        for (int i = 0; i < n; i++)
        {
            pdu[6 + 2 * i] = (byte)((ushort)values[i] >> 8);
            pdu[7 + 2 * i] = (byte)values[i];
        }
        Exchange(pdu);
    }

    /// <summary>Send one request PDU, return the matching response PDU (function code first).</summary>
    byte[] Exchange(byte[] pdu)
    {
        if (stream == null) throw new InvalidOperationException("Not connected");

        ushort tid = ++transactionId;
        ushort length = (ushort)(pdu.Length + 1); // + unit id

        byte[] frame = new byte[7 + pdu.Length];
        frame[0] = Hi(tid); frame[1] = Lo(tid);
        frame[2] = 0; frame[3] = 0;                  // protocol id: always 0 for Modbus
        frame[4] = Hi(length); frame[5] = Lo(length);
        frame[6] = unitId;
        Buffer.BlockCopy(pdu, 0, frame, 7, pdu.Length);
        stream.Write(frame, 0, frame.Length);

        byte[] header = ReadExactly(7);
        ushort respTid = (ushort)((header[0] << 8) | header[1]);
        int respLength = (header[4] << 8) | header[5];
        if (respTid != tid) throw new Exception($"Transaction id mismatch (sent {tid}, got {respTid})");
        if (respLength < 2 || respLength > 254) throw new Exception($"Bad response length {respLength}");

        byte[] resp = ReadExactly(respLength - 1);
        if ((resp[0] & 0x80) != 0)
            throw new Exception($"PLC returned Modbus exception {resp[1]} for function {resp[0] & 0x7F} " +
                                "(2 = illegal address: check the Address Mapping Reference)");
        if (resp[0] != pdu[0]) throw new Exception($"Function code mismatch (sent {pdu[0]}, got {resp[0]})");
        return resp;
    }

    byte[] ReadExactly(int count)
    {
        var buf = new byte[count];
        int offset = 0;
        while (offset < count)
        {
            int n = stream.Read(buf, offset, count - offset);
            if (n <= 0) throw new Exception("Connection closed by PLC");
            offset += n;
        }
        return buf;
    }

    static byte Hi(ushort v) => (byte)(v >> 8);
    static byte Lo(ushort v) => (byte)v;
}
