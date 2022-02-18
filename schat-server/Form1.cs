using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;

namespace schat_server
{
    public partial class Form1 : Form
    {
        TcpListener server;
        int PORT = 9999;
        IPAddress serverip = IPAddress.Parse("192.168.0.243");
        Thread serverConThread;
        List<TcpClient> clients;
        List<Thread> processClientMessageThread;
        bool serverWorking = false;
        public Form1()
        {
            InitializeComponent();
        }

        //stop server btn click
        private void button2_Click(object sender, EventArgs e)
        {
            stopServer();
        }

        private void stopServer()
        {
            serverWorking = false;
            pictureBox1.BackColor = Color.Red;
            sendServerEndingMessage(); //trimitem un mesaj de final tuturor userilor conectati 
            serverConThread.Suspend(); //oprim thread-ul care face listening pt noi conexiuni 
            closeClientListeningThreads();  //oprim restul thread-urilor 
            closeClientConnections(); //inchidem restul conexiunilor cu clientii 
            server.Stop();  //oprim server-ul
            listBox1.Items.Clear(); //golim lista de useri 
            processClientMessageThread.Clear(); //golim lista de thread-uri
            clients.Clear(); //golim lista de clienti
            //punem in log faptul ca s-a oprit serverul si momentul la care s-a oprit
            richTextBox1.AppendText("\n Server stopped at " + DateTime.Now.ToString() + "\n");
        }

        void closeClientConnections()
        {
            foreach (TcpClient client in clients)
            {
                if (client.Connected)
                {
                    client.GetStream().Close();
                    client.Close();
                }
            }
        }

        void closeClientListeningThreads()
        {
            foreach (Thread t in processClientMessageThread)
            {
                t.Suspend();
            }
        }

        //start server
        private void button1_Click(object sender, EventArgs e)
        {
            pictureBox1.BackColor = Color.Green;
            progressBar1.Value = 0;
            richTextBox1.AppendText("Server started at " + DateTime.Now.ToString() + "\n");
            serverWorking = true;
            server.Start(); //pornim server-ul 
            
            serverConThread = new Thread(waitForConnections);
            serverConThread.Start();//pornim thread-ul care asculta conexiunile
        }

        //metoda corespunzatoare acceptului de conexiuni
        void waitForConnections()
        { 
            while (true)
            {
                Console.WriteLine("Waiting for connection");
                TcpClient client = server.AcceptTcpClient(); 
                //dupa ce un client  se conecteaza  
                clients.Add(client); //adaugam clientul in lista 
                //adaugam thread-ul corespunzator clientului in lista de thread-ului 
                processClientMessageThread.Add(new Thread(() => processClientMessage(client)));
                processClientMessageThread[processClientMessageThread.Count-1].Start(); //pornim thread-ul userului conectat
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            AllocConsole(); //pt a vizualiza consola
            //initializam obiectele necesare
            server = new TcpListener(serverip, PORT);
            clients = new List<TcpClient>();
            processClientMessageThread = new List<Thread>();
            pictureBox1.BackColor = Color.Red;
        }

        //metoda care se ocupa de management-ul unui user 
        //adica asculta ce mesaje trimite si le face broadcast si la ceilalti
        void processClientMessage(TcpClient client)
        {
            string username = ""; //username-ul userului procesat
            listBox1.Invoke((MethodInvoker)delegate
            {
                username = getUsername(client); //preluam de la user, username-ul
                listBox1.Items.Add(username); 
            });

            broadcast(username + "  a intrat pe server \n"); //anuntam si ceilalti useri ca a intrat user-ul curent pe server

            richTextBox1.Invoke((MethodInvoker)delegate //executie pe thread-ul in care a fost creat richtextbox
            {
                //afisez in richtextbox date despre noul client conectat
                richTextBox1.AppendText("Connect new client on " + DateTime.Now.ToString() + " username : " + username);
                richTextBox1.AppendText(" ### IP : " + client.Client.RemoteEndPoint.ToString() + "\n");
            });

            while (true) 
            {
                //get the stream from the client 
                NetworkStream stream = client.GetStream();
                int i;
                Byte[] bytes = new Byte[256];
                String data;
                // Loop to receive all the data sent by the client.
                while ((i = stream.Read(bytes, 0, 256)) != 0)
                {
                    // Translate data bytes to a ASCII string.
                    data = Encoding.ASCII.GetString(bytes, 0, i);
                    Console.WriteLine("Received: {0} from {1}", data, client.Client.LocalEndPoint.ToString());

                    if(data == "/outserver") //daca mesajul primit de la client e ca sa anunte serverul ca s-a deconectat
                    {
                        int index = clients.IndexOf(client);
                        //remove client from list
                        clients.Remove(client);
                        //add in log that a user disconnected
                        richTextBox1.Invoke((MethodInvoker)delegate
                        {
                            richTextBox1.AppendText("Disconnected client " + listBox1.Items[index]);
                            richTextBox1.AppendText("---" + DateTime.Now.ToString() + client.Client.RemoteEndPoint.ToString() + "\n");
                        });
                        //broadcas that this user left
                        broadcast("A iesit " +  listBox1.Items[index].ToString() +" de pe server", client);
                        //close the connection 
                        client.Close();
                        //delete user from listbox
                        listBox1.Items.RemoveAt(index);
                        //oprim thread-ul curent deoarece nu mai trebuie procesate mesaje (deoarece userul s-a deconectat)
                        Thread.CurrentThread.Suspend();
                    }
                    else //daca e un mesaj normal, pur si simplu i facem broadcast
                        broadcast(data, client);
                }
            }

        }
        
        //send too all users the message | exclude client 
        void broadcast(string message, TcpClient client = null)
        {
            try
            {
                byte[] msg = Encoding.ASCII.GetBytes(message.ToCharArray(), 0, message.Length);
                foreach (TcpClient c in clients)
                {
                    if (c.Connected)
                    {
                        NetworkStream stream = c.GetStream();
                        if (c != client || client == null) //daca e diferit de client-ul dat sau nu e mentionat clientul 
                        {
                            stream.Write(msg, 0, msg.Length);
                        }
                        else
                        {
                            if(client != null && message.Length > 0)
                            {
                                message = "Eu : " + message;
                                byte[] msg2 = Encoding.ASCII.GetBytes(message.ToCharArray(), 0, message.Length);
                                stream.Write(msg2, 0, msg2.Length);
                            }
                        }

                        
                    }
                }
            }
            catch(Exception e)
            {
                MessageBox.Show(e.Message);
            }
            
        }
        //metoda care trimite mesajul de inchidere server la useri 
        void sendServerEndingMessage()
        {
            int counter = 3; //nr de secunde pana se inchide serverul 
            while (counter > 0)
            {
                broadcast("Server inchis in " + counter.ToString() + "\n");
                counter--;
                progressBar1.Value += 33;
                Thread.Sleep(1000); //sleep 1 secunda
            }
            broadcast("server inchis");
        }


        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        private void label2_Click(object sender, EventArgs e)
        {

        }

        //get the username from a certain tcpClient 
        string getUsername(TcpClient client)
        {
            NetworkStream networkStream = client.GetStream();
            byte[] message = new byte[255];
            networkStream.Read(message, 0, 255);
            string username = Encoding.UTF8.GetString(message, 0, message.Length);
            return username.Trim();
        }

        //kick a certain user
        private void button3_Click(object sender, EventArgs e)
        {
            int selectedUsernameIndex = listBox1.SelectedIndex;//stochez index-ul userului selectat
            if(selectedUsernameIndex > -1) //daca a fost selectat un user
            {
                try
                {

                    string message = listBox1.SelectedItem.ToString();

                    richTextBox1.AppendText(message);
                    richTextBox1.AppendText(" a fost dat afara de pe server \n");

                    NetworkStream s = clients[selectedUsernameIndex].GetStream(); //preiau stream-ul userului selectat
                    //trimite mesaj la user cu kick
                    byte[] buffer = new byte[5]; 
                    buffer = Encoding.UTF8.GetBytes("kick".ToCharArray(), 0, 4);
                    s.Write(buffer, 0, buffer.Length);
                    //inchidem socket-ul corespunzator
                    clients[selectedUsernameIndex].Close();
                    //stergem socket-ul din lista
                    clients.RemoveAt(selectedUsernameIndex);
                    //suspendam thread-ul care asteapta mesaje de la acest user
                    processClientMessageThread[selectedUsernameIndex].Suspend();
                    //stergem thread-ul din lista de thread-uri
                    processClientMessageThread.RemoveAt(selectedUsernameIndex);
                    listBox1.Items.RemoveAt(selectedUsernameIndex); //stergem din listbox
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
            else
            {
                MessageBox.Show("Nu a fost selectat niciun user");
            }
            
           
        }

        //export the log
        private void button4_Click(object sender, EventArgs e)
        {
            saveFileDialog1.DefaultExt = "log";
            saveFileDialog1.Title = "Save the log file on your drive";
            saveFileDialog1.Filter = "Text File | *.log";

            if(saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                StreamWriter writer = new StreamWriter(saveFileDialog1.FileName);
                try
                {
                    writer.Write(richTextBox1.Text);
                    writer.Close();
                    MessageBox.Show("log saved");
                }
                catch (IOException ex)
                {
                    MessageBox.Show(ex.Message);
                }
                
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (serverWorking)
            {
                MessageBox.Show("Atentie ! Server-ul se va opri !!!");
                stopServer();
            }
        }

        private void richTextBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
           
        }

        private void richTextBox1_KeyDown(object sender, KeyEventArgs e)
        {
            MessageBox.Show("Nu ai voie sa scrii !!!");
        }
    }
}
