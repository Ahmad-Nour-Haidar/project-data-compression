namespace ProjectDataCompression.Functions;

using System.Windows.Forms;

public static class PasswordDialog
{
    public static string RequestPassword(string prompt = "Enter Password")
    {
        Form promptForm = new Form()
        {
            Width = 300,
            Height = 150,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = prompt,
            StartPosition = FormStartPosition.CenterScreen,
            MinimizeBox = false,
            MaximizeBox = false
        };

        Label textLabel = new Label() { Left = 20, Top = 20, Text = prompt, AutoSize = true };

        TextBox passwordBox = new TextBox() { Left = 20, Top = 50, Width = 240, PlaceholderText = "Password" };
        passwordBox.UseSystemPasswordChar = true;

        Button confirmation = new Button()
            { Text = "OK", Left = 100, Width = 80, Top = 80, DialogResult = DialogResult.OK };
        confirmation.Click += (_, _) => { promptForm.Close(); };

        promptForm.Controls.Add(textLabel);
        promptForm.Controls.Add(passwordBox);
        promptForm.Controls.Add(confirmation);
        promptForm.AcceptButton = confirmation;

        return promptForm.ShowDialog() == DialogResult.OK ? passwordBox.Text : "";
    }
}