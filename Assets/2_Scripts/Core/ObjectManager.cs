using System;
using System.Collections.Generic;
using UnityEngine;

public class ObjectManager : MonoBehaviour
{
    // 씬 어디서든 ObjectManager.Instance로 접근하기 위한 싱글톤
    public static ObjectManager Instance { get; private set; }

    // 오브젝트 하나가 낼 수 있는 사운드 묶음. 인스펙터에서 objects 리스트와 같은 순서로 채워 넣음
    [Serializable]
    public class ObjectSoundSet
    {
        public AudioClip[] clickSounds; // 클릭할 때 랜덤 재생할 사운드들
        public AudioClip breakSound; // 파괴될 때 재생할 사운드
    }

    [SerializeField] private ObjectSoundSet[] objectSounds; // objects 리스트와 같은 순서/개수로 채워야 함 (26개)

    // 오브젝트 하나의 체력 단계별 스프라이트 묶음. 인스펙터에서 objects 리스트와 같은 순서로 채워 넣음
    [Serializable]
    public class ObjectVisualSet
    {
        public Sprite[] healthStages; // 체력 100% -> 0% 순서 (예: Object1, Object1-1, Object1-2, Object1-3)
        // 크기는 코드에서 계산하지 않음 - 각 이미지의 Pixels Per Unit(Import Settings)으로 직접 맞출 것
    }

    [SerializeField] private ObjectVisualSet[] objectVisuals; // objects 리스트와 같은 순서/개수로 채워야 함 (26개)

    // 파괴 대상 오브젝트 
    private static readonly List<ObjectData> objects = new List<ObjectData>
    {
        // 오브젝트 색 조절
        new ObjectData(1, 1, "Plate", new Color(0.95f, 0.95f, 0.92f)),
        new ObjectData(1, 2, "Glass Cup", new Color(0.93f, 0.95f, 0.96f)),
        new ObjectData(1, 3, "Flower Pot", new Color(0.80f, 0.42f, 0.25f)),
        new ObjectData(2, 1, "Window", new Color(0.60f, 0.80f, 0.90f)),
        new ObjectData(2, 2, "Wooden Chair", new Color(0.55f, 0.35f, 0.20f)),
        new ObjectData(2, 3, "Wooden Desk", new Color(0.50f, 0.30f, 0.15f)),
        new ObjectData(3, 1, "Brick Wall", new Color(0.70f, 0.30f, 0.20f)),
        new ObjectData(3, 2, "Door", new Color(0.40f, 0.25f, 0.15f)),
        new ObjectData(3, 3, "Side Table", new Color(0.60f, 0.45f, 0.30f)),
        new ObjectData(4, 1, "Wardrobe", new Color(0.35f, 0.22f, 0.12f)),
        new ObjectData(4, 2, "Refrigerator", new Color(0.90f, 0.90f, 0.90f)),
        new ObjectData(4, 3, "Sofa", new Color(0.40f, 0.45f, 0.55f)),
        new ObjectData(5, 1, "Bathroom", new Color(0.80f, 0.90f, 0.95f)),
        new ObjectData(5, 2, "Studio Apartment", new Color(0.75f, 0.70f, 0.60f)),
        new ObjectData(5, 3, "Apartment", new Color(0.60f, 0.60f, 0.60f)),
        new ObjectData(6, 1, "Low-rise Building", new Color(0.55f, 0.55f, 0.58f)),
        new ObjectData(6, 2, "High-rise Building", new Color(0.45f, 0.50f, 0.60f)),
        new ObjectData(6, 3, "City Block", new Color(0.50f, 0.50f, 0.50f)),
        new ObjectData(7, 1, "Entire City", new Color(0.40f, 0.40f, 0.45f)),
        new ObjectData(7, 2, "Metropolis", new Color(0.30f, 0.30f, 0.40f)),
        new ObjectData(8, 1, "Mountain Range", new Color(0.50f, 0.45f, 0.40f)),
        new ObjectData(8, 2, "Continent", new Color(0.40f, 0.50f, 0.30f)),
        new ObjectData(9, 1, "Planet (Earth-class)", new Color(0.30f, 0.50f, 0.70f)),
        new ObjectData(9, 2, "Gas Giant", new Color(0.80f, 0.60f, 0.30f)),
        new ObjectData(10, 1, "Star System", new Color(0.90f, 0.80f, 0.30f)),
        new ObjectData(10, 2, "Galaxy", new Color(0.40f, 0.20f, 0.60f)),
    };

    private int equippedIndex; // 실제로 선택된 오브젝트의 objects 리스트 인덱스 (0부터 시작)

    public int ObjectCount => objects.Count; // UI에서 화살표로 둘러볼 때 범위 계산용
    public int EquippedIndex => equippedIndex; // 지금 선택된 오브젝트의 인덱스 (UI에서 "Equipped" 표시용)
    public ObjectData CurrentObject => objects[equippedIndex];

    // 선택된 오브젝트가 바뀔 때마다(=Equip 호출 시) 새 오브젝트 데이터를 전달 - 오브젝트 UI 등이 구독
    public event Action<ObjectData> OnObjectChanged;

    void Awake()
    {
        // 씬에 ObjectManager가 중복으로 존재하지 않도록 방지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // 인스펙터에 넣어둔 사운드들을 같은 순서의 오브젝트 데이터에 채워 넣음
        for (int i = 0; i < objects.Count && objectSounds != null && i < objectSounds.Length; i++)
        {
            objects[i].clickSounds = objectSounds[i].clickSounds;
            objects[i].breakSound = objectSounds[i].breakSound;
        }

        // 인스펙터에 넣어둔 스프라이트 단계들을 같은 순서의 오브젝트 데이터에 채워 넣음
        for (int i = 0; i < objects.Count && objectVisuals != null && i < objectVisuals.Length; i++)
            objects[i].healthStages = objectVisuals[i].healthStages;
    }

    // 인덱스로 오브젝트 데이터를 조회만 함 (선택은 안 함) - UI가 화살표로 둘러볼 때 사용
    public ObjectData GetObjectAt(int index) => objects[index];

    // 지금은 제한 없이 자유롭게 선택 가능 (WeaponManager와 동일한 정책)
    public void Equip(int index)
    {
        index = Mathf.Clamp(index, 0, objects.Count - 1);
        equippedIndex = index;
        OnObjectChanged?.Invoke(CurrentObject);
    }
}
