using Unity.Netcode.Components;

namespace ZombieSurvival
{
    /// <summary>
    /// NetworkTransform, которым управляет владелец объекта (игрок двигает себя сам — без задержки ввода).
    /// </summary>
    public class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
