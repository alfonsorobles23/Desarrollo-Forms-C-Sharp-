using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WiseMSP;

namespace WiseFlasher
{
    public partial class Form2 : Form
    {
        Form1 formulario1;
        public Form2(Form1 formulario1)
        {
            InitializeComponent();
            this.formulario1 = formulario1;
        }
        private byte ParseByteSafe(TextBox textBox, string fieldName)
        {
            if (!string.IsNullOrWhiteSpace(textBox.Text))
            {
                try
                {
                    return Byte.Parse(textBox.Text);
                }
                catch (FormatException)
                {
                    MessageBox.Show($"Por favor, ingrese un valor válido para {fieldName}.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch (OverflowException)
                {
                    MessageBox.Show($"El valor ingresado para {fieldName} es demasiado grande para un byte.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show($"El campo {fieldName} no puede estar vacío.", "Advertencia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return 0; // Valor por defecto si falla la conversión
        }

        private UInt16 ParseUInt16Safe(TextBox textBox, string fieldName)
        {
            if (!string.IsNullOrWhiteSpace(textBox.Text))
            {
                try
                {
                    return UInt16.Parse(textBox.Text);
                }
                catch (Exception)
                {
                    MessageBox.Show($"Por favor, ingrese un valor válido para {fieldName}.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            return 0;
        }

        private UInt32 ParseUInt32Safe(TextBox textBox, string fieldName)
        {
            if (!string.IsNullOrWhiteSpace(textBox.Text))
            {
                try
                {
                    return UInt32.Parse(textBox.Text);
                }
                catch (Exception)
                {
                    MessageBox.Show($"Por favor, ingrese un valor válido para {fieldName}.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            return 0;
        }

        private UInt64 ParseUInt64Safe(TextBox textBox, string fieldName)
        {
            if (!string.IsNullOrWhiteSpace(textBox.Text))
            {
                try
                {
                    return UInt64.Parse(textBox.Text);
                }
                catch (Exception)
                {
                    MessageBox.Show($"Por favor, ingrese un valor válido para {fieldName}.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            return 0;
        }

        private void Form2_Load(object sender, EventArgs e)
        {

        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }
        // Llamada desde el segundo formulario
        private async void LlamarFuncionDesdeSegundoFormulario()
        {
            try
            {
                await Task.Run(() => formulario1.Write_XBEE_Parameters_Click());
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ocurrió un error: " + ex.Message);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            LlamarFuncionDesdeSegundoFormulario();
            Program.varglobal.router = ParseByteSafe(textBox2, "Router");
            Program.varglobal.lone_coord = ParseByteSafe(textBox6, "Lone Coord");
            Program.varglobal.identifier = ParseByteSafe(textBox4, "Identifier");
            Program.varglobal.mod_Config = ParseUInt16Safe(textBox11, "Mod Config");
            Program.varglobal.Channel = ParseByteSafe(textBox3, "Channel");
            Program.varglobal.MeshNetworkRetries = ParseByteSafe(textBox5, "Mesh Network Retries");
            Program.varglobal.ChannelMask = ParseUInt64Safe(textBox7, "Channel Mask");
            Program.varglobal.NetworkHops = ParseByteSafe(textBox8, "Network Hops");
            Program.varglobal.NetworkDelaySlots = ParseByteSafe(textBox9, "Network Delay Slots");
            Program.varglobal.UnicastMacRetries = ParseByteSafe(textBox10, "Unicast MAC Retries");
            Program.varglobal.SleepTime = ParseUInt32Safe(textBox15, "Sleep Time");
            Program.varglobal.WakeTime = ParseUInt32Safe(textBox14, "Wake Time");
            Program.varglobal.SleepOptions = ParseByteSafe(textBox13, "Sleep Options");
            Program.varglobal.SleepMode = ParseByteSafe(textBox12, "Sleep Mode");
            Program.varglobal.PowerLevel = ParseByteSafe(textBox16, "Power Level");
            Program.varglobal.coordinator = ParseByteSafe(textBox17, "Coordinator");
            Program.varglobal.SoloGW = ParseByteSafe(textBox18, "Solo GW");
            Program.varglobal.PreambleID = ParseByteSafe(textBox19, "Preamble ID");
            Program.varglobal.SecurityEnable = ParseByteSafe(textBox20, "Security Enable");


        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void label7_Click(object sender, EventArgs e)
        {

        }

        private void label13_Click(object sender, EventArgs e)
        {

        }
    }
}
