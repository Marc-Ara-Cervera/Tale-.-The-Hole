using UnityEngine;

public interface iDañable
{
    // Método básico para recibir daño
    void TakeDamage(float amount, GameObject instigator = null);

    //Método básico para curar
    void Heal(float amount);

    // Verificar si la entidad está viva
    //bool IsAlive();
}
