function Start-AltPowMiner (
	[string]$MinerId,
	[double]$HPF
)
{
	Start-Process -FilePath ../Miner.exe -ArgumentList "--name $MinerId", "--hash-power-factor $HPF" -WindowStyle Hidden
}

function Stop-AllAltPowMiners
{
	Param (
		[Parameter(Mandatory=$false, ParameterSetName='A')][switch]$All,
		[Parameter(Mandatory=$false, ParameterSetName='B')][string]$MinerId

	)
	if ($All.IsPresent -eq $true) {
		Stop-Process -Name "Miner" -Force
	}
	else {
		
	}
}

function Start-Miners 
{
	Param (
		[Parameter(Mandatory=$false, ParameterSetName='A')][int]$Count,
		[Parameter(Mandatory=$false, ParameterSetName='A')][double]$HashPower,
		[Parameter(Mandatory=$false, ParameterSetName='B')][double[]]$HashPowers
	)
    
    if($PSCmdlet.ParameterSetName -eq 'A')
    {
        for($i = 0; $i -lt $Count; $i++)
        {
            Start-AltPowMiner -MinerId "M$($i.ToString().PadLeft(2, '0'))" -HPF $HashPower
        }
    }
    else {
        $i = 0;
        foreach($factor in $HashPowers)
        {
            Start-AltPowMiner -MinerId "M$($i.ToString().PadLeft(2, '0'))" -HPF $factor
            $i++;
        }
    }
}