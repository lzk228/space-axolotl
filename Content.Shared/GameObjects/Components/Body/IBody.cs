#nullable enable
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.GameObjects.Components.Body.Part;
using Content.Shared.GameObjects.Components.Damage;

namespace Content.Shared.GameObjects.Components.Body
{
    public interface IBody : IDamageableComponent, IBodyPartContainer
    {
        public string? TemplateName { get; }

        public string? PresetName { get; }

        // TODO tf is this
        /// <summary>
        ///     Maps all parts on this template to its BodyPartType.
        ///     For instance, "right arm" is mapped to "BodyPartType.arm" on the humanoid
        ///     template.
        /// </summary>
        public Dictionary<string, BodyPartType> Slots { get; }

        /// <summary>
        ///     Maps slots to the part filling each one.
        /// </summary>
        public IReadOnlyDictionary<string, IBodyPart> Parts { get; }

        // TODO what am i doing
        /// <summary>
        ///     Maps limb name to the list of their connections to other limbs.
        ///     For instance, on the humanoid template "torso" is mapped to a list
        ///     containing "right arm", "left arm", "left leg", and "right leg".
        ///     This is mapped both ways during runtime, but in the prototype only one
        ///     way has to be defined, i.e., "torso" to "left arm" will automatically
        ///     map "left arm" to "torso".
        /// </summary>
        public Dictionary<string, HashSet<string>> Connections { get; }

        /// <summary>
        ///     Maps a template slot to the ID of the <see cref="IBodyPart"/>
        ///     that should fill it. E.g. "right arm" : "BodyPart.arm.basic_human".
        /// </summary>
        public IReadOnlyDictionary<string, string> PartIds { get; }

        public IReadOnlyDictionary<string, string> MechanismLayers { get; }

        /// <summary>
        ///     Installs the given <see cref="IBodyPart"/> into the given slot.
        /// </summary>
        /// <returns>True if successful, false otherwise.</returns>
        bool TryAddPart(string slot, IBodyPart part, bool force = false);

        bool HasPart(string slot);

        /// <summary>
        ///     Removes the given <see cref="IBodyPart"/> reference, potentially
        ///     dropping other <see cref="IBodyPart">BodyParts</see> if they
        ///     were hanging off of it.
        /// </summary>
        void RemovePart(IBodyPart part, bool drop);

        /// <summary>
        ///     Removes the body part in slot <see cref="slot"/> from this body,
        ///     if one exists.
        /// </summary>
        /// <param name="slot">The slot to remove it from.</param>
        /// <param name="drop">
        ///     Whether or not to drop the removed <see cref="IBodyPart"/>.
        /// </param>
        /// <returns>True if the part was removed, false otherwise.</returns>
        bool RemovePart(string slot, bool drop);

        /// <summary>
        ///     Removes the body part from this body, if one exists.
        /// </summary>
        /// <param name="part">The part to remove from this body.</param>
        /// <param name="slotName">The slot that the part was in, if any.</param>
        /// <returns>True if <see cref="part"/> was removed, false otherwise.</returns>
        bool RemovePart(IBodyPart part, [NotNullWhen(true)] out string? slotName);

        /// <summary>
        ///     Disconnects the given <see cref="IBodyPart"/> reference, potentially
        ///     dropping other <see cref="IBodyPart">BodyParts</see> if they
        ///     were hanging off of it.
        /// </summary>
        /// <param name="part">The part to drop.</param>
        /// <param name="dropped">
        ///     All of the parts that were dropped, including <see cref="part"/>.
        /// </param>
        /// <returns>
        ///     True if the part was dropped, false otherwise.
        /// </returns>
        bool TryDropPart(IBodyPart part, [NotNullWhen(true)] out List<IBodyPart>? dropped);

        /// <summary>
        ///     Recursively searches for if <see cref="part"/> is connected to
        ///     the center.
        /// </summary>
        /// <param name="part">The body part to find the center for.</param>
        /// <returns>True if it is connected to the center, false otherwise.</returns>
        bool ConnectedToCenter(IBodyPart part);

        /// <summary>
        ///     Finds the central <see cref="IBodyPart"/>, if any, of this body based on
        ///     the <see cref="BodyTemplate"/>. For humans, this is the torso.
        /// </summary>
        /// <returns>The <see cref="BodyPart"/> if one exists, null otherwise.</returns>
        IBodyPart? CenterPart();

        /// <summary>
        ///     Returns whether the given part slot name exists within the current
        ///     <see cref="BodyTemplate"/>.
        /// </summary>
        /// <param name="slot">The slot to check for.</param>
        /// <returns>True if the slot exists in this body, false otherwise.</returns>
        bool HasSlot(string slot);

        /// <summary>
        ///     Finds the <see cref="IBodyPart"/> in the given <see cref="slot"/> if
        ///     one exists.
        /// </summary>
        /// <param name="slot">The part slot to search in.</param>
        /// <param name="result">The body part in that slot, if any.</param>
        /// <returns>True if found, false otherwise.</returns>
        bool TryGetPart(string slot, [NotNullWhen(true)] out IBodyPart? result);

        /// <summary>
        ///     Finds the slotName that the given <see cref="IBodyPart"/> resides in.
        /// </summary>
        /// <param name="part">
        ///     The <see cref="IBodyPart"/> to find the slot for.
        /// </param>
        /// <param name="slot">The slot found, if any.</param>
        /// <returns>True if a slot was found, false otherwise</returns>
        bool TryGetSlot(IBodyPart part, [NotNullWhen(true)] out string? slot);

        /// <summary>
        ///     Finds the <see cref="BodyPartType"/> in the given
        ///     <see cref="slot"/> if one exists.
        /// </summary>
        /// <param name="slot">The slot to search in.</param>
        /// <param name="result">
        ///     The <see cref="BodyPartType"/> of that slot, if any.
        /// </param>
        /// <returns>True if found, false otherwise.</returns>
        bool TryGetSlotType(string slot, out BodyPartType result);

        /// <summary>
        ///     Finds the names of all slots connected to the given
        ///     <see cref="slot"/> for the template.
        /// </summary>
        /// <param name="slot">The slot to search in.</param>
        /// <param name="connections">The connections found, if any.</param>
        /// <returns>True if the connections are found, false otherwise.</returns>
        bool TryGetSlotConnections(string slot, [NotNullWhen(true)] out HashSet<string>? connections);

        /// <summary>
        ///     Grabs all occupied slots connected to the given slot,
        ///     regardless of whether the given <see cref="slot"/> is occupied.
        /// </summary>
        /// <param name="slot">The slot name to find connections from.</param>
        /// <param name="connections">The connected body parts, if any.</param>
        /// <returns>
        ///     True if successful, false if the slot couldn't be found on this body.
        /// </returns>
        bool TryGetPartConnections(string slot, [NotNullWhen(true)] out List<IBodyPart>? connections);

        /// <summary>
        ///     Grabs all parts connected to the given <see cref="part"/>, regardless
        ///     of whether the given <see cref="part"/> is occupied.
        /// </summary>
        /// <param name="part">The part to find connections from.</param>
        /// <param name="connections">The connected body parts, if any.</param>
        /// <returns>
        ///     True if successful, false if the part couldn't be found on this body.
        /// </returns>
        bool TryGetPartConnections(IBodyPart part, [NotNullWhen(true)] out List<IBodyPart>? connections);

        /// <summary>
        ///     Grabs all <see cref="IBodyPart"/> of the given type in this body.
        /// </summary>
        List<IBodyPart> GetPartsOfType(BodyPartType type);
    }
}
