using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Steppe.Caravan
{
    public enum CaravanMaterialNetworkKind
    {
        Biomass,
        Mechanical
    }

    public enum CaravanMaterialPortRole
    {
        WetBiomassOutput,
        WetBiomassInput,
        DryBiomassOutput,
        DryBiomassInput,
        MechanicalSource,
        TransmissionInput,
        TransmissionOutput,
        MechanicalConsumer,
        CouplingEndpoint
    }

    [DisallowMultipleComponent]
    public sealed class CaravanMaterialPort : MonoBehaviour
    {
        private readonly List<CaravanMaterialLink> links =
            new List<CaravanMaterialLink>(2);
        private GameObject buildMarker;
        private Renderer markerRenderer;
        private Light markerLight;
        private MaterialPropertyBlock markerProperties;
        private Vector3 markerBaseScale;
        private int maximumConnections = 1;

        public CaravanPart Part { get; private set; }
        public CaravanMaterialPortRole Role { get; private set; }
        public CaravanMaterialNetworkKind NetworkKind { get; private set; }
        public string PortId { get; private set; }
        public int ConnectedLinkCount => links.Count;
        public int MaximumConnections => maximumConnections;
        public bool IsAtCapacity => ConnectedLinkCount >= MaximumConnections;
        public bool IsBuildMarkerVisible =>
            buildMarker != null && buildMarker.activeSelf;

        public void Configure(
            CaravanPart part,
            CaravanMaterialPortRole role,
            GameObject communicationBuildMarker = null,
            int connectionCapacity = 1,
            string portId = null)
        {
            Part = part != null
                ? part
                : throw new ArgumentNullException(nameof(part));
            Role = role;
            NetworkKind = CaravanConnectionRules.GetNetworkKind(role);
            maximumConnections = Mathf.Max(1, connectionCapacity);
            var module = Part.GetComponent<CaravanModule>();
            PortId = string.IsNullOrWhiteSpace(portId)
                ? $"{module?.InstanceId ?? Part.name}:{NetworkKind}:{Role}"
                : portId;
            buildMarker = communicationBuildMarker;
            if (buildMarker != null)
            {
                markerRenderer = buildMarker.GetComponent<Renderer>();
                markerLight = buildMarker.GetComponent<Light>();
                markerProperties = new MaterialPropertyBlock();
                markerBaseScale = buildMarker.transform.localScale;
                buildMarker.SetActive(false);
            }
        }

        internal void Register(CaravanMaterialLink link)
        {
            if (link != null && !links.Contains(link))
            {
                links.Add(link);
            }
        }

        internal void Unregister(CaravanMaterialLink link)
        {
            if (link != null)
            {
                links.Remove(link);
            }
        }

        public void SetBuildPresentation(
            bool visible,
            bool selected,
            bool compatible,
            bool focused)
        {
            if (buildMarker == null)
            {
                return;
            }

            buildMarker.SetActive(visible);
            if (!visible)
            {
                return;
            }

            var color = selected
                ? new Color(1f, 0.68f, 0.08f, 1f)
                : !compatible
                    ? new Color(0.95f, 0.12f, 0.08f, 1f)
                    : focused
                        ? new Color(0.18f, 1f, 0.48f, 1f)
                        : NetworkKind == CaravanMaterialNetworkKind.Biomass
                            ? new Color(0.54f, 0.86f, 0.18f, 1f)
                            : new Color(0.86f, 0.58f, 0.18f, 1f);
            if (markerRenderer != null)
            {
                markerRenderer.GetPropertyBlock(markerProperties);
                markerProperties.SetColor("_BaseColor", color);
                markerProperties.SetColor("_Color", color);
                markerRenderer.SetPropertyBlock(markerProperties);
            }
            if (markerLight != null)
            {
                markerLight.color = color;
                markerLight.intensity = selected || focused ? 0.9f : 0.45f;
            }

            buildMarker.transform.localScale = markerBaseScale
                                               * (selected || focused ? 1.35f : 1f);
        }

    }

    [DisallowMultipleComponent]
    public sealed class CaravanMaterialLink : MonoBehaviour
    {
        private CaravanMaterialPort start;
        private CaravanMaterialPort end;
        private Transform routingRoot;
        private LineRenderer line;
        private float activity;

        public CaravanMaterialPort Start => start;
        public CaravanMaterialPort End => end;
        public float Activity => activity;
        public bool IsConductive =>
            isActiveAndEnabled
            && start != null
            && end != null
            && start.isActiveAndEnabled
            && end.isActiveAndEnabled
            && IsRopeEndpointOperational(start)
            && IsRopeEndpointOperational(end);

        public void Configure(
            CaravanMaterialPort startPort,
            CaravanMaterialPort endPort,
            Transform networkRoot,
            Material material)
        {
            start = startPort != null
                ? startPort
                : throw new ArgumentNullException(nameof(startPort));
            end = endPort != null
                ? endPort
                : throw new ArgumentNullException(nameof(endPort));
            routingRoot = networkRoot != null
                ? networkRoot
                : throw new ArgumentNullException(nameof(networkRoot));
            line = gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 4;
            line.widthMultiplier = start.NetworkKind
                                   == CaravanMaterialNetworkKind.Biomass
                ? 0.11f
                : 0.075f;
            line.numCapVertices = 5;
            line.numCornerVertices = 4;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            start.Register(this);
            end.Register(this);
            RefreshRoute();
        }

        public void SetActivity(float normalizedActivity)
        {
            activity = Mathf.Clamp01(normalizedActivity);
            if (line != null)
            {
                var baseWidth = start.NetworkKind
                                == CaravanMaterialNetworkKind.Biomass
                    ? 0.095f
                    : 0.065f;
                line.widthMultiplier = baseWidth
                                       * Mathf.Lerp(0.9f, 1.35f, activity);
            }
        }

        private void LateUpdate()
        {
            RefreshRoute();
        }

        private void RefreshRoute()
        {
            if (line == null)
            {
                return;
            }

            line.enabled = IsConductive;
            if (!line.enabled)
            {
                return;
            }

            var startLocal = routingRoot.InverseTransformPoint(
                start.transform.position);
            var endLocal = routingRoot.InverseTransformPoint(
                end.transform.position);
            const float routeY = 0.18f;
            line.SetPosition(0, start.transform.position);
            line.SetPosition(
                1,
                routingRoot.TransformPoint(
                    new Vector3(startLocal.x, routeY, startLocal.z)));
            line.SetPosition(
                2,
                routingRoot.TransformPoint(
                    new Vector3(endLocal.x, routeY, endLocal.z)));
            line.SetPosition(3, end.transform.position);
        }

        private void OnDestroy()
        {
            start?.Unregister(this);
            end?.Unregister(this);
        }

        private static bool IsRopeEndpointOperational(
            CaravanMaterialPort port)
        {
            if (port.Role != CaravanMaterialPortRole.CouplingEndpoint)
            {
                return true;
            }

            var rope =
                port.Part.GetComponent<CaravanCouplingRopeModule>();
            return rope != null && !rope.IsBroken;
        }
    }

    public sealed class CaravanMaterialNetwork : MonoBehaviour
    {
        private readonly List<CaravanMaterialPort> ports =
            new List<CaravanMaterialPort>(16);
        private readonly List<CaravanMaterialLink> links =
            new List<CaravanMaterialLink>(16);
        private Material linkMaterial;

        public CaravanMaterialNetworkKind Kind { get; private set; }
        public IReadOnlyList<CaravanMaterialPort> Ports => ports;
        public IReadOnlyList<CaravanMaterialLink> Links => links;
        public int LinkCount => links.Count;
        public event Action ConnectionsChanged;

        public void Configure(
            CaravanMaterialNetworkKind networkKind,
            Material material)
        {
            Kind = networkKind;
            linkMaterial = material != null
                ? material
                : throw new ArgumentNullException(nameof(material));
        }

        public int RegisterModule(CaravanModule module)
        {
            if (module == null)
            {
                return 0;
            }

            var modulePorts =
                module.GetComponentsInChildren<CaravanMaterialPort>(true);
            var added = 0;
            for (var index = 0; index < modulePorts.Length; index++)
            {
                if (RegisterPort(modulePorts[index]))
                {
                    added++;
                }
            }

            return added;
        }

        public bool RegisterPort(CaravanMaterialPort port)
        {
            if (port == null
                || port.Part == null
                || port.NetworkKind != Kind
                || ports.Contains(port))
            {
                return false;
            }

            ports.Add(port);
            return true;
        }

        public bool UnregisterPort(CaravanMaterialPort port)
        {
            if (port == null || !ports.Contains(port))
            {
                return false;
            }

            DisconnectPort(port);
            ports.Remove(port);
            return true;
        }

        public bool CanConnect(
            CaravanMaterialPort first,
            CaravanMaterialPort second)
        {
            if (first == null
                || second == null
                || first == second
                || first.Part == second.Part
                || first.NetworkKind != Kind
                || second.NetworkKind != Kind
                || !ports.Contains(first)
                || !ports.Contains(second)
                || first.IsAtCapacity
                || second.IsAtCapacity
                || FindLink(first, second) != null)
            {
                return false;
            }

            return CaravanConnectionRules.AreCompatible(
                Kind,
                first.Role,
                second.Role);
        }

        public bool TryConnect(
            CaravanMaterialPort first,
            CaravanMaterialPort second)
        {
            if (!CanConnect(first, second))
            {
                return false;
            }

            var linkObject = new GameObject(
                $"{first.Role} to {second.Role} Link");
            linkObject.transform.SetParent(transform, false);
            var link = linkObject.AddComponent<CaravanMaterialLink>();
            link.Configure(first, second, transform, linkMaterial);
            LinkCouplingRopes(first, second);
            links.Add(link);
            ConnectionsChanged?.Invoke();
            return true;
        }

        public int DisconnectPort(CaravanMaterialPort port)
        {
            if (port == null || !ports.Contains(port))
            {
                return 0;
            }

            var removed = 0;
            for (var index = links.Count - 1; index >= 0; index--)
            {
                var link = links[index];
                if (link != null && (link.Start == port || link.End == port))
                {
                    RemoveLink(link);
                    removed++;
                }
            }

            return removed;
        }

        public bool RemoveLink(CaravanMaterialLink link)
        {
            if (link == null || !links.Remove(link))
            {
                return false;
            }

            link.Start?.Unregister(link);
            link.End?.Unregister(link);
            UnlinkCouplingRopes(link.Start, link.End);
            Destroy(link.gameObject);
            ConnectionsChanged?.Invoke();
            return true;
        }

        public void ClearConnections()
        {
            for (var index = links.Count - 1; index >= 0; index--)
            {
                RemoveLink(links[index]);
            }
        }

        public bool AreDirectlyConnected(
            CaravanPart first,
            CaravanPart second)
        {
            if (first == null || second == null)
            {
                return false;
            }

            for (var index = 0; index < links.Count; index++)
            {
                var link = links[index];
                if (link != null
                    && link.IsConductive
                    && ((link.Start.Part == first && link.End.Part == second)
                        || (link.Start.Part == second
                            && link.End.Part == first)))
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasPath(CaravanPart first, CaravanPart second)
        {
            if (first == null || second == null)
            {
                return false;
            }
            if (first == second)
            {
                return true;
            }

            var visited = new HashSet<CaravanPart> { first };
            var pending = new Queue<CaravanPart>();
            pending.Enqueue(first);
            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                for (var index = 0; index < links.Count; index++)
                {
                    var link = links[index];
                    if (link == null || !link.IsConductive)
                    {
                        continue;
                    }

                    CaravanPart adjacent = null;
                    if (link.Start.Part == current)
                    {
                        adjacent = link.End.Part;
                    }
                    else if (link.End.Part == current)
                    {
                        adjacent = link.Start.Part;
                    }

                    if (adjacent == null || !visited.Add(adjacent))
                    {
                        continue;
                    }
                    if (adjacent == second)
                    {
                        return true;
                    }
                    pending.Enqueue(adjacent);
                }
            }

            return false;
        }

        public void SetLinkActivity(
            CaravanPart first,
            CaravanPart second,
            float normalizedActivity)
        {
            for (var index = 0; index < links.Count; index++)
            {
                var link = links[index];
                if (link != null
                    && ((link.Start.Part == first && link.End.Part == second)
                        || (link.Start.Part == second
                            && link.End.Part == first)))
                {
                    link.SetActivity(normalizedActivity);
                }
            }
        }

        private CaravanMaterialLink FindLink(
            CaravanMaterialPort first,
            CaravanMaterialPort second)
        {
            for (var index = 0; index < links.Count; index++)
            {
                var link = links[index];
                if (link != null
                    && ((link.Start == first && link.End == second)
                        || (link.Start == second && link.End == first)))
                {
                    return link;
                }
            }

            return null;
        }

        private static void LinkCouplingRopes(
            CaravanMaterialPort first,
            CaravanMaterialPort second)
        {
            if (first.Role != CaravanMaterialPortRole.CouplingEndpoint
                || second.Role != CaravanMaterialPortRole.CouplingEndpoint)
            {
                return;
            }

            var firstRope =
                first.Part.GetComponent<CaravanCouplingRopeModule>();
            var secondRope =
                second.Part.GetComponent<CaravanCouplingRopeModule>();
            firstRope?.LinkTo(secondRope);
        }

        private static void UnlinkCouplingRopes(
            CaravanMaterialPort first,
            CaravanMaterialPort second)
        {
            if (first == null
                || second == null
                || first.Role != CaravanMaterialPortRole.CouplingEndpoint
                || second.Role != CaravanMaterialPortRole.CouplingEndpoint)
            {
                return;
            }

            first.Part.GetComponent<CaravanCouplingRopeModule>()?.Unlink();
        }
    }
}
