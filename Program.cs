using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace CSharpService
{
    public class TrayApp : Form
    {
        public static NotifyIcon trayIcon;
        private ContextMenu trayMenu;
        private ClientWebSocket clientWebSocket;
        private CancellationTokenSource webSocketCancelToken;

        public TrayApp()
        {
            trayMenu = new ContextMenu();
            trayMenu.MenuItems.Add("Exit", OnExit);

            trayIcon = new NotifyIcon();
            trayIcon.Text = "C# Service Application";
            trayIcon.Icon = Properties.Resources.Icon1;
            trayIcon.ContextMenu = trayMenu;
            trayIcon.Visible = true;

            webSocketCancelToken = new CancellationTokenSource();
            ConnectWebSocket();

            Application.Run(this);
        }

        private async void ConnectWebSocket()
        {
            clientWebSocket = new ClientWebSocket();
            try
            {
                await clientWebSocket.ConnectAsync(new Uri("ws://localhost:8000/ws/advanced"), CancellationToken.None);
                trayIcon.ShowBalloonTip(2000, "WebSocket Service", "Connected", ToolTipIcon.Info);
                await Task.Factory.StartNew(WebSocketLoop, webSocketCancelToken.Token);

            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "WebSocket Service Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task WebSocketLoop()
        {
            while (!webSocketCancelToken.Token.IsCancellationRequested && clientWebSocket.State == WebSocketState.Open)
            {
                try
                {
                    // Perform ping-pong operations                 
                    await SendMessage("ping");

                    var buffer = new byte[1024];
                    var result = await clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                    }
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);                      
                        await ProcessMessage(message);

                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: " + ex.Message, "Service Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    await clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                    trayIcon.ShowBalloonTip(2000, "WebSocket Service", "Connection closed", ToolTipIcon.Info);
                }
            }
        }

        private async Task SendMessage(string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            await clientWebSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task ProcessMessage(string message)
        {
            //Process received message and query SQL Server database
            var connectionString = "Dsn=MSQLODBC;uid=root";
            using (var connection = new OdbcConnection(connectionString))
            {
                connection.Open();
                var command = new OdbcCommand("SELECT * FROM actor", connection);
                var dataTable = new DataTable();
                using (var adapter = new OdbcDataAdapter(command))
                {
                    adapter.Fill(dataTable);
                }

                //Convert DataTable to JSON
                var json = ConvertDataTableToJson(dataTable);

                //Send JSON data to FastAPI WebSocket
                await SendMessage(json);

            }
        }


        private string ConvertDataTableToJson(DataTable dataTable)
        {
            string jsonData;
            jsonData = JsonConvert.SerializeObject(dataTable);
            return jsonData;
        }


        private void OnExit(object sender, EventArgs e)
        {
            trayIcon.ShowBalloonTip(2000, "WebSocket Service", "Disonnected", ToolTipIcon.Info);
            webSocketCancelToken.Cancel();
            clientWebSocket.Dispose();
            trayIcon.Visible = false;
            Application.Exit();

        }
    }
    static class Program
    {

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApp());
            }
            catch { }


        }
    }
}
