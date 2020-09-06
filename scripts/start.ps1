function Start-AltPowMiner (
	[string]$MinerId,
	[double]$HPF
)
{
	Start-Process -FilePath ../Miner.exe -ArgumentList "--name $MinerId", "--hash-power-factor $HPF"
}

function Stop-AllAltPowMiners
{
	Param (
		[Parameter(Mandatory=$false, ParameterSetName='A')][switch]$All,
		[Parameter(Mandatory=$false, ParameterSetName='B')][string]$MinerId

	)
	if ($All.IsPresent -eq $true) {
		Stop-Process -Name "Miner.exe" -Force
	}
	else {
		
	}
}