# OIDC Server

## Development

### Migrations
Add migration commands:
```
dotnet ef migrations add Init -c AuthDbContext -o Infrastructure/Data/Migrations
```

### Generate self-signed certificate for data protection
``` 
openssl req -x509 -newkey rsa:2048 -keyout dp-key.pem -out dp-cert.pem -days 1825 -nodes -subj "/CN=DataProtection"
openssl pkcs12 -export -out dp-cert.pfx -inkey dp-key.pem -in dp-cert.pem -passout pass:your-password
[Convert]::ToBase64String([IO.File]::ReadAllBytes("dp-cert.pfx")) | Out-File -Encoding ASCII "dp-cert-base64.txt"
```