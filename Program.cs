﻿using System.Net.Sockets;
using System.Net;
using System.Text;
using System.IO.Compression;

internal class Program
{
    private static void Main(string[] args)
    {
        TcpListener server = new(IPAddress.Any, 4221);
        Console.WriteLine(server.LocalEndpoint.ToString());
        try
        {
            server.Start();
            while (true)
            {
                TcpClient client = server.AcceptTcpClient();
                NetworkStream stream = client.GetStream();

                byte[] buffer = new byte[1024];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);

                string[] lines = Encoding.ASCII.GetString(buffer).Split("\r\n");
                Console.WriteLine($"Http request lines:\r\n{string.Join(' ', lines)}");
                string[] firsLineParts = lines[0].Split(" ");
                Console.WriteLine($"First line parts:\r\n{string.Join(' ', firsLineParts)}");
                string[] thirdLineParts = lines[2].Split(" ");
                Console.WriteLine($"Third line parts:\r\n{string.Join(' ', thirdLineParts)}");

                var (method, path, httpVer) = (firsLineParts[0], firsLineParts[1], firsLineParts[2]);
                string response = string.Empty;
                byte[] compressedByte = new byte[1024];

                if (path == "/")
                {
                    response = "HTTP/1.1 200 OK\r\n\r\n";
                }
                else if (path.StartsWith("/echo/"))
                {
                    string message = path.Substring(6);
                    if (thirdLineParts[0] == "Accept-Encoding:" && (thirdLineParts[1..].Contains("gzip,") || thirdLineParts[1..].Contains("gzip")))
                    {
                        byte[] byteStr = Encoding.ASCII.GetBytes(message);
                        compressedByte = GzipCompressor.Compress(byteStr);
                        Console.WriteLine(Encoding.ASCII.GetString(compressedByte));
                        response = $"HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Encoding: gzip\r\nContent-Length: {compressedByte.Length}\r\n\r\n";
                    }
                    else
                    {
                        response = $"HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: {message.Length}\r\n\r\n{message}";
                    }
                }
                else if (path == "/user-agent")
                {
                    string message = thirdLineParts[1];
                    response = $"HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: {message.Length}\r\n\r\n{message}";
                }
                else if (path.StartsWith("/files/"))
                {
                    string[] argv = Environment.GetCommandLineArgs();
                    Console.WriteLine($"Current CLI args:\r\n{string.Join(" ", argv)}");
                    string fileName = path[7..];
                    Console.WriteLine($"Filename:\r\n{fileName}");
                    string currDir = argv[2];
                    Console.WriteLine($"currDir:\r\n{currDir}");
                    string filePath = currDir + fileName;
                    Console.WriteLine($"filePath:\r\n{filePath}");

                    if (method == "GET")
                    {
                        if (File.Exists(filePath))
                        {
                            string fileContent = File.ReadAllText(filePath);
                            response = $"HTTP/1.1 200 OK\r\nContent-Type: application/octet-stream\r\nContent-Length: {fileContent.Length}\r\n\r\n{fileContent}";
                        }
                        else
                        {
                            response = "HTTP/1.1 404 Not Found\r\n\r\n";
                        }
                    }
                    else
                    {
                        Console.WriteLine("Method is POST");
                        File.CreateText(filePath);
                        Console.WriteLine("File has been created");
                        string fileContent = lines[^1].Trim('\0');
                        Console.WriteLine($"FileContents:\r\n{fileContent}");
                        File.AppendAllText(filePath, fileContent);
                        response = "HTTP/1.1 201 Created\r\n\r\n";
                    }
                }
                else
                {
                    response = "HTTP/1.1 404 Not Found\r\n\r\n";
                }
                Console.WriteLine($"Response string:\r\n{response}");
                stream.Write(Encoding.ASCII.GetBytes(response).Concat(compressedByte).ToArray());

                client.Close();
            }
        }
        catch (SocketException e)
        {
            Console.WriteLine("SocketException: {0}", e.Message);
        }
    }
}

public static class GzipCompressor
{
    public static byte[] Compress(byte[] data)
    {
        using (var compressedStream = new MemoryStream())
        {
            using (var zipStream = new GZipStream(compressedStream, CompressionMode.Compress, true))
            {
                zipStream.Write(data, 0, data.Length);
                zipStream.Close();
                return compressedStream.ToArray();
            }
        }
    }
}
